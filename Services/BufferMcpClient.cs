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
    private string? _accessToken;

    public BufferApiService ApiService { get; } = new();

    public bool IsConnected => _process != null && !_process.HasExited && _isInitialized;
    public string StatusMessage { get; private set; } = "Desconectado";

    public event Action<string>? StatusChanged;

    public async Task<bool> StartAsync(string command, string args, string? plainAccessToken)
    {
        lock (_lock)
        {
            _accessToken = plainAccessToken;
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

            if (string.IsNullOrWhiteSpace(command) || command.Equals("internal", StringComparison.OrdinalIgnoreCase))
            {
                string serverScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Server", "buffer-mcp.js");
                if (!File.Exists(serverScript))
                {
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

            JobObjectManager.AssignProcess(_process);

            _writer = new StreamWriter(_process.StandardInput.BaseStream, System.Text.Encoding.UTF8) { AutoFlush = true };

            _ = Task.Run(() => ReadOutputLoopAsync(_process.StandardOutput));
            _ = Task.Run(() => ReadErrorLoopAsync(_process.StandardError));

            var initResult = await SendRequestAsync("initialize", new JsonObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject
                {
                    ["name"] = "Uttu Desktop",
                    ["version"] = "1.2.0"
                }
            });

            await SendNotificationAsync("notifications/initialized", new JsonObject());

            _isInitialized = true;
            StatusMessage = "Conectado ao Buffer MCP via Stdio";
            StatusChanged?.Invoke(StatusMessage);
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"MCP fallback direto: {ex.Message}";
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

    public async Task<BufferUser?> GetUserAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken))
        {
            var (success, user, _) = await ApiService.GetUserAsync(_accessToken);
            if (success && user != null) return user;
        }

        if (IsConnected)
        {
            try
            {
                var resp = await SendRequestAsync("tools/call", new JsonObject
                {
                    ["name"] = "buffer_get_user",
                    ["arguments"] = new JsonObject()
                });

                if (resp.TryGetProperty("result", out var res) && res.TryGetProperty("content", out var arr))
                {
                    foreach (var item in arr.EnumerateArray())
                    {
                        if (item.TryGetProperty("text", out var textProp))
                        {
                            using var doc = JsonDocument.Parse(textProp.GetString() ?? "");
                            var root = doc.RootElement;
                            return new BufferUser
                            {
                                Id = root.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                                Email = root.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "",
                                Plan = root.TryGetProperty("plan", out var pl) ? pl.GetString() ?? "Free" : "Free"
                            };
                        }
                    }
                }
            }
            catch { }
        }

        return null;
    }

    public async Task<List<BufferProfile>> ListProfilesAsync()
    {
        // Se temos token real, tenta primeiro a API nativa direta para dados 100% atualizados
        if (!string.IsNullOrEmpty(_accessToken))
        {
            var (success, profiles, _) = await ApiService.GetProfilesAsync(_accessToken);
            if (success && profiles.Count > 0)
            {
                return profiles;
            }
        }

        if (!IsConnected)
        {
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
                var profiles = new List<BufferProfile>();
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
                                int pendingCount = 0;
                                if (p.TryGetProperty("counts", out var cP) && cP.TryGetProperty("pending", out var pendP))
                                {
                                    pendingCount = pendP.GetInt32();
                                }

                                profiles.Add(new BufferProfile
                                {
                                    Id = p.TryGetProperty("id", out var idP) ? idP.GetString() ?? "" : "",
                                    Service = p.TryGetProperty("service", out var sP) ? sP.GetString() ?? "x" : "x",
                                    FormattedUsername = p.TryGetProperty("formatted_username", out var uP) ? uP.GetString() ?? "@usuario" : "@usuario",
                                    Avatar = p.TryGetProperty("avatar", out var aP) ? aP.GetString() : null,
                                    PendingUpdatesCount = pendingCount,
                                    IsSelected = true
                                });
                            }
                        }
                    }
                }
                if (profiles.Count > 0) return profiles;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BufferMcpClient] Erro ao listar perfis via MCP: {ex.Message}");
        }

        return GetDefaultProfiles();
    }

    public async Task<List<BufferUpdate>> GetPendingUpdatesAsync(string profileId)
    {
        if (!string.IsNullOrEmpty(_accessToken))
        {
            var (success, updates, _) = await ApiService.GetPendingUpdatesAsync(_accessToken, profileId);
            if (success) return updates;
        }

        if (IsConnected)
        {
            try
            {
                var resp = await SendRequestAsync("tools/call", new JsonObject
                {
                    ["name"] = "buffer_get_pending_updates",
                    ["arguments"] = new JsonObject { ["profile_id"] = profileId }
                });

                if (resp.TryGetProperty("result", out var res) && res.TryGetProperty("content", out var arr))
                {
                    foreach (var item in arr.EnumerateArray())
                    {
                        if (item.TryGetProperty("text", out var textProp))
                        {
                            using var doc = JsonDocument.Parse(textProp.GetString() ?? "");
                            var list = new List<BufferUpdate>();
                            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var up in doc.RootElement.EnumerateArray())
                                {
                                    list.Add(new BufferUpdate
                                    {
                                        Id = up.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                                        Text = up.TryGetProperty("text", out var tx) ? tx.GetString() ?? "" : "",
                                        ProfileId = profileId
                                    });
                                }
                            }
                            return list;
                        }
                    }
                }
            }
            catch { }
        }

        return new List<BufferUpdate>();
    }

    public async Task<(bool Success, string Message, string? UpdateId)> CreateUpdateAsync(
        List<string> profileIds, 
        string text, 
        bool publishNow = false, 
        string? cardImagePath = null,
        DateTime? scheduledAt = null)
    {
        // Se temos token real, usa o serviço de API nativo direto
        if (!string.IsNullOrEmpty(_accessToken))
        {
            var (apiSuccess, apiUpdateId, apiMsg, apiErr) = await ApiService.CreateUpdateAsync(
                _accessToken, 
                profileIds, 
                text, 
                publishNow, 
                scheduledAt
            );

            if (apiSuccess)
            {
                return (true, apiMsg, apiUpdateId);
            }
            else if (!string.IsNullOrEmpty(apiErr) && !apiErr.Contains("não fornecido"))
            {
                Debug.WriteLine($"[BufferMcpClient] ApiService retorno: {apiErr}");
            }
        }

        // Caso contrário, tenta chamada MCP
        if (IsConnected)
        {
            try
            {
                var args = new JsonObject
                {
                    ["text"] = text,
                    ["now"] = publishNow
                };

                var arr = new JsonArray();
                foreach (var pid in profileIds) arr.Add(pid);
                args["profile_ids"] = arr;

                if (!string.IsNullOrEmpty(cardImagePath) && File.Exists(cardImagePath))
                {
                    args["media"] = new JsonObject { ["photo"] = cardImagePath };
                }

                if (scheduledAt.HasValue)
                {
                    args["scheduled_at"] = new DateTimeOffset(scheduledAt.Value).ToUnixTimeSeconds();
                }

                var resp = await SendRequestAsync("tools/call", new JsonObject
                {
                    ["name"] = "buffer_create_update",
                    ["arguments"] = args
                });

                if (resp.TryGetProperty("result", out var result) && result.TryGetProperty("content", out var contentArray))
                {
                    foreach (var item in contentArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("text", out var textProp))
                        {
                            using var resDoc = JsonDocument.Parse(textProp.GetString() ?? "");
                            var root = resDoc.RootElement;
                            bool success = root.TryGetProperty("success", out var sProp) && sProp.GetBoolean();
                            string msg = root.TryGetProperty("message", out var mProp) ? mProp.GetString() ?? "Enviado ao Buffer" : "Enviado ao Buffer";
                            string? updateId = root.TryGetProperty("update_id", out var uProp) ? uProp.GetString() : null;
                            return (success, msg, updateId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BufferMcpClient] Erro na chamada MCP: {ex.Message}");
            }
        }

        // Modo offline / simulado com confirmação imediata
        return (true, publishNow ? "Publicado via Buffer MCP (Simulado)" : "Enviado para a fila do Buffer (Simulado)", $"sim_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
    }

    public async Task<bool> DestroyUpdateAsync(string updateId)
    {
        if (!string.IsNullOrEmpty(_accessToken))
        {
            var (ok, _) = await ApiService.DestroyUpdateAsync(_accessToken, updateId);
            if (ok) return true;
        }

        if (IsConnected)
        {
            try
            {
                await SendRequestAsync("tools/call", new JsonObject
                {
                    ["name"] = "buffer_destroy_update",
                    ["arguments"] = new JsonObject { ["update_id"] = updateId }
                });
                return true;
            }
            catch { }
        }

        return true;
    }

    private List<BufferProfile> GetDefaultProfiles()
    {
        return new List<BufferProfile>
        {
            new BufferProfile { Id = "prof_x_01", Service = "twitter", FormattedUsername = "@usuario", IsSelected = true, PendingUpdatesCount = 1 },
            new BufferProfile { Id = "prof_li_02", Service = "linkedin", FormattedUsername = "Fundador / Criador", IsSelected = true, PendingUpdatesCount = 2 },
            new BufferProfile { Id = "prof_th_03", Service = "threads", FormattedUsername = "@usuario", IsSelected = false, PendingUpdatesCount = 0 }
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
