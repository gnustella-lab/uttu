using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Uttu.Models;

namespace Uttu.Services;

public class BufferMcpClient : IDisposable
{
    private Process? _process;
    private StreamWriter? _writer;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pendingRequests = new();
    private long _nextId = 1;
    private readonly object _lock = new();
    private bool _isInitialized;
    private bool _isDisposed;

    public bool IsConnected => _process != null && !_process.HasExited && _isInitialized;
    public string StatusMessage { get; private set; } = "Desconectado";

    public event Action<string>? StatusChanged;

    public async Task<bool> StartAsync(string command, string args, string? plainAccessToken)
    {
        lock (_lock)
        {
            if (IsConnected)
                return true;

            StopInternal();
        }

        try
        {
            StatusMessage = "Iniciando servidor Buffer MCP...";
            StatusChanged?.Invoke(StatusMessage);

            string exe;
            string commandArgs;

            // Resolve command
            if (string.IsNullOrWhiteSpace(command) || command.Equals("internal", StringComparison.OrdinalIgnoreCase))
            {
                // Default to bundled server via node
                string serverScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Server", "buffer-mcp.js");
                if (!File.Exists(serverScript))
                {
                    // Fallback relative to project if running in dev
                    serverScript = Path.Combine(Directory.GetCurrentDirectory(), "Server", "buffer-mcp.js");
                }

                exe = "node";
                commandArgs = $"\"{serverScript}\"";
            }
            else
            {
                exe = command;
                commandArgs = args ?? string.Empty;
            }

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = commandArgs,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            };

            if (!string.IsNullOrEmpty(plainAccessToken))
            {
                psi.EnvironmentVariables["BUFFER_ACCESS_TOKEN"] = plainAccessToken;
            }

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.Exited += (s, e) =>
            {
                StatusMessage = "Processo MCP encerrado";
                StatusChanged?.Invoke(StatusMessage);
            };

            if (!_process.Start())
            {
                StatusMessage = "Falha ao iniciar processo MCP";
                StatusChanged?.Invoke(StatusMessage);
                return false;
            }

            // Assign child process to Windows Job Object to prevent orphan processes
            JobObjectManager.AssignProcess(_process);

            _writer = new StreamWriter(_process.StandardInput.BaseStream, System.Text.Encoding.UTF8) { AutoFlush = true };

            // Start background reader
            _ = Task.Run(() => ReadOutputLoopAsync(_process.StandardOutput));
            _ = Task.Run(() => ReadErrorLoopAsync(_process.StandardError));

            // Perform MCP initialize handshake
            var initResult = await SendRequestAsync("initialize", new JsonObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject
                {
                    ["name"] = "Uttu Desktop",
                    ["version"] = "1.0.0"
                }
            });

            // Send notification initialized
            await SendNotificationAsync("notifications/initialized", new JsonObject());

            _isInitialized = true;
            StatusMessage = "Conectado ao Buffer MCP via Stdio";
            StatusChanged?.Invoke(StatusMessage);
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro MCP: {ex.Message}";
            StatusChanged?.Invoke(StatusMessage);
            return false;
        }
    }

    private async Task ReadOutputLoopAsync(StreamReader reader)
    {
        try
        {
            while (!_isDisposed && !reader.EndOfStream)
            {
                string? line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement.Clone();

                    if (root.TryGetProperty("id", out var idProp) && idProp.TryGetInt64(out long id))
                    {
                        if (_pendingRequests.TryRemove(id, out var tcs))
                        {
                            tcs.TrySetResult(root);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[BufferMcpClient] Erro ao parsear linha MCP: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BufferMcpClient] ReadOutputLoopAsync encerrado: {ex.Message}");
        }
    }

    private async Task ReadErrorLoopAsync(StreamReader reader)
    {
        try
        {
            while (!_isDisposed && !reader.EndOfStream)
            {
                string? line = await reader.ReadLineAsync();
                if (!string.IsNullOrEmpty(line))
                {
                    Debug.WriteLine($"[MCP-STDERR] {line}");
                }
            }
        }
        catch { }
    }

    private async Task<JsonElement> SendRequestAsync(string method, JsonNode? parameters = null, int timeoutMs = 8000)
    {
        long id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[id] = tcs;

        var requestNode = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method
        };

        if (parameters != null)
        {
            requestNode["params"] = parameters;
        }

        string json = requestNode.ToJsonString();

        lock (_lock)
        {
            if (_writer == null)
            {
                _pendingRequests.TryRemove(id, out _);
                throw new InvalidOperationException("Servidor MCP não está conectado.");
            }
            _writer.WriteLine(json);
        }

        using var cts = new CancellationTokenSource(timeoutMs);
        using var reg = cts.Token.Register(() =>
        {
            if (_pendingRequests.TryRemove(id, out var pending))
            {
                pending.TrySetException(new TimeoutException($"A requisição MCP '{method}' atingiu tempo limite."));
            }
        });

        return await tcs.Task;
    }

    private async Task SendNotificationAsync(string method, JsonNode? parameters = null)
    {
        var node = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method
        };
        if (parameters != null)
        {
            node["params"] = parameters;
        }

        string json = node.ToJsonString();
        lock (_lock)
        {
            if (_writer != null)
            {
                _writer.WriteLine(json);
            }
        }
        await Task.CompletedTask;
    }

    public async Task<List<BufferProfile>> ListProfilesAsync()
    {
        var profiles = new List<BufferProfile>();

        if (!IsConnected)
        {
            // Fallback default profiles for offline / pre-connection view
            return GetDefaultProfiles();
        }

        try
        {
            var callParams = new JsonObject
            {
                ["name"] = "buffer_list_profiles",
                ["arguments"] = new JsonObject()
            };

            var resp = await SendRequestAsync("tools/call", callParams);
            if (resp.TryGetProperty("result", out var result) && result.TryGetProperty("content", out var contentArray))
            {
                foreach (var item in contentArray.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var textProp))
                    {
                        string textJson = textProp.GetString() ?? "";
                        using var innerDoc = JsonDocument.Parse(textJson);
                        if (innerDoc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var p in innerDoc.RootElement.EnumerateArray())
                            {
                                profiles.Add(new BufferProfile
                                {
                                    Id = p.TryGetProperty("id", out var idP) ? idP.GetString() ?? "" : "",
                                    Service = p.TryGetProperty("service", out var sP) ? sP.GetString() ?? "x" : "x",
                                    FormattedUsername = p.TryGetProperty("formatted_username", out var uP) ? uP.GetString() ?? "@usuario" : "@usuario",
                                    Avatar = p.TryGetProperty("avatar", out var aP) ? aP.GetString() : null,
                                    IsSelected = true
                                });
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BufferMcpClient] Erro ao listar perfis: {ex.Message}");
            return GetDefaultProfiles();
        }

        return profiles.Count > 0 ? profiles : GetDefaultProfiles();
    }

    public async Task<(bool Success, string Message, string? UpdateId)> CreateUpdateAsync(
        List<string> profileIds, 
        string text, 
        bool publishNow = false, 
        string? cardImagePath = null)
    {
        if (!IsConnected)
        {
            return (true, "Modo Simulado: Post registrado com sucesso na fila do Buffer.", $"sim_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
        }

        try
        {
            var args = new JsonObject
            {
                ["text"] = text,
                ["now"] = publishNow
            };

            var arr = new JsonArray();
            foreach (var pid in profileIds)
            {
                arr.Add(pid);
            }
            args["profile_ids"] = arr;

            if (!string.IsNullOrEmpty(cardImagePath) && File.Exists(cardImagePath))
            {
                var mediaNode = new JsonObject
                {
                    ["photo"] = cardImagePath
                };
                args["media"] = mediaNode;
            }

            var callParams = new JsonObject
            {
                ["name"] = "buffer_create_update",
                ["arguments"] = args
            };

            var resp = await SendRequestAsync("tools/call", callParams);
            if (resp.TryGetProperty("result", out var result) && result.TryGetProperty("content", out var contentArray))
            {
                foreach (var item in contentArray.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var textProp))
                    {
                        string responseText = textProp.GetString() ?? "";
                        using var resDoc = JsonDocument.Parse(responseText);
                        var root = resDoc.RootElement;
                        bool success = root.TryGetProperty("success", out var sProp) && sProp.GetBoolean();
                        string msg = root.TryGetProperty("message", out var mProp) ? mProp.GetString() ?? "Enviado ao Buffer" : "Enviado ao Buffer";
                        string? updateId = root.TryGetProperty("update_id", out var uProp) ? uProp.GetString() : null;
                        return (success, msg, updateId);
                    }
                }
            }

            return (true, publishNow ? "Publicado via Buffer MCP" : "Enviado para a fila do Buffer", null);
        }
        catch (Exception ex)
        {
            return (false, $"Erro na chamada MCP: {ex.Message}", null);
        }
    }

    private List<BufferProfile> GetDefaultProfiles()
    {
        return new List<BufferProfile>
        {
            new BufferProfile { Id = "prof_x_01", Service = "twitter", FormattedUsername = "@usuario", IsSelected = true },
            new BufferProfile { Id = "prof_li_02", Service = "linkedin", FormattedUsername = "Fundador / Criador", IsSelected = true },
            new BufferProfile { Id = "prof_th_03", Service = "threads", FormattedUsername = "@usuario", IsSelected = false }
        };
    }

    private void StopInternal()
    {
        _isInitialized = false;
        try
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill(true);
            }
            _process?.Dispose();
            _process = null;
        }
        catch { }

        _writer?.Dispose();
        _writer = null;

        foreach (var kvp in _pendingRequests)
        {
            kvp.Value.TrySetCanceled();
        }
        _pendingRequests.Clear();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        StopInternal();
    }
}
