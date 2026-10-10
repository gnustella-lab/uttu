using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Uttu.Models;

namespace Uttu.Services;

public class IdeaGeneratorService
{
    private readonly HttpClient _httpClient;

    public IdeaGeneratorService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
    }

    public async Task<List<PostDraft>> GenerateBatchAsync(List<MemoryItem> activeMemories, VoiceProfile voiceProfile, AppSettings settings)
    {
        // Try calling external LLM if configured and token is present
        string plainApiKey = SecurityService.Unprotect(settings.AiApiKeyEncrypted);
        if (!string.IsNullOrWhiteSpace(plainApiKey) && !settings.AiProvider.StartsWith("Local", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var llmDrafts = await TryGenerateWithLlmAsync(plainApiKey, activeMemories, voiceProfile, settings);
                if (llmDrafts != null && llmDrafts.Count > 0)
                {
                    return llmDrafts;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IdeaGeneratorService] Erro ao chamar LLM: {ex.Message}. Alternando para motor nativo heurístico.");
            }
        }

        // Native Heuristic Engine (offline, deterministic, instant, high fidelity)
        return GenerateWithNativeEngine(activeMemories, voiceProfile);
    }

    public async Task<string> RefineDraftAsync(PostDraft draft, string instruction, List<MemoryItem> activeMemories, VoiceProfile voiceProfile, AppSettings settings)
    {
        string plainApiKey = SecurityService.Unprotect(settings.AiApiKeyEncrypted);
        if (!string.IsNullOrWhiteSpace(plainApiKey) && !settings.AiProvider.StartsWith("Local", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var llmRefined = await TryRefineWithLlmAsync(plainApiKey, draft, instruction, activeMemories, settings);
                if (!string.IsNullOrWhiteSpace(llmRefined))
                {
                    return llmRefined.Trim();
                }
            }
            catch { }
        }

        // Heuristic refiner
        return RefineWithNativeEngine(draft, instruction);
    }

    private List<PostDraft> GenerateWithNativeEngine(List<MemoryItem> activeMemories, VoiceProfile voiceProfile)
    {
        var result = new List<PostDraft>();
        var random = new Random();

        // Archetypes of high-performing engineering/product thoughts
        var archetypes = new[]
        {
            new
            {
                Theme = "Menos código e poda de complexidade",
                HookX = "O código mais rápido de debugar é aquele que nunca foi escrito.",
                BodyX = "Antes de adicionar um novo microserviço ou uma dependência pesada no repositório, pergunte: dá para resolver podando código existente?\n\nMenos superfícies = menos falhas.",
                HookLi = "A maioria das equipes sofre por excesso de arquitetura, não por falta dela.",
                BodyLi = "Na última semana decidimos podar três telas do fluxo de onboarding e remover duas dependências antigas.\n\nO resultado?\n\n- Queda de 40% nos chamados de suporte;\n- Onboarding 3x mais rápido;\n- Zero novas linhas para manter.\n\nUm produto excelente não precisa de mais funcionalidades para crescer. Ele precisa de mais clareza."
            },
            new
            {
                Theme = "Entrega pragmática vs perfeccionismo teórico",
                HookX = "Software perfeito em staging não gera nenhum valor real.",
                BodyX = "Coloque uma versão sólida e compacta na mão do cliente hoje. O feedback de 5 minutos de uso real vale mais que 3 semanas de reunião de alinhamento.",
                HookLi = "Teoria de arquitetura é linda no papel. Em produção, simplicidade vence sempre.",
                BodyLi = "Já vi times gastarem meses desenhando sistemas distribuídos ultra-complexos para resolver um problema que um simples SQLite ou fila local resolveria com elegância.\n\nAntes de tentar escalar para 10 milhões de requisições:\n\n1. Entregue valor tangível hoje;\n2. Meça gargalos reais com telemetria;\n3. Otimize apenas o que dói.\n\nPragmatismo é o melhor design pattern."
            },
            new
            {
                Theme = "Decisões de produto human-in-the-loop",
                HookX = "Automação sem critério humano gera ruído em escala.",
                BodyX = "A melhor inteligência artificial não substitui a decisão final de quem conhece o contexto. Ela gera opções; você aprova com um clique.",
                HookLi = "Por que o modelo 'Human-in-the-loop' supera qualquer automação 100% cega.",
                BodyLi = "Criar automações é fácil. O problema é quando elas geram alucinações e conteúdo genérico sem o filtro de quem vive a rotina do negócio.\n\nO segredo da IA aplicada:\n\n- Deixe o algoritmo fazer o trabalho pesado de pesquisa e ideação;\n- Mantenha a alavanca de aprovação e refinamento nas suas mãos;\n- Publique apenas o que passa pelo seu padrão de qualidade.\n\nControle e velocidade podem andar juntos."
            }
        };

        // Pick 2 or 3 archetypes
        var selected = archetypes.OrderBy(_ => random.Next()).Take(2).ToList();

        foreach (var arch in selected)
        {
            // Draft para X (compacto, <= 280 caracteres)
            string contentX = $"{arch.HookX}\n\n{arch.BodyX}".Trim();
            if (contentX.Length > 280)
            {
                contentX = contentX.Substring(0, 276) + "...";
            }

            result.Add(new PostDraft
            {
                Theme = arch.Theme,
                Platform = "X",
                Content = contentX,
                Status = "Draft",
                CreatedAt = DateTime.Now
            });

            // Draft para LinkedIn (estruturado, com espaçamento vertical)
            string contentLi = $"{arch.HookLi}\n\n{arch.BodyLi}".Trim();
            result.Add(new PostDraft
            {
                Theme = arch.Theme,
                Platform = "LinkedIn",
                Content = contentLi,
                Status = "Draft",
                CreatedAt = DateTime.Now
            });
        }

        return result;
    }

    private string RefineWithNativeEngine(PostDraft draft, string instruction)
    {
        string text = draft.Content.Trim();
        string lowerInst = instruction.ToLowerInvariant();

        if (lowerInst.Contains("conciso") || lowerInst.Contains("curto") || lowerInst.Contains("resum"))
        {
            // Condensa removendo linhas secundárias
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 2)
            {
                return $"{lines[0]}\n\n{lines[^1]}";
            }
            return text;
        }

        if (lowerInst.Contains("gancho") || lowerInst.Contains("impacto") || lowerInst.Contains("início"))
        {
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (lines.Count > 0)
            {
                lines[0] = "A maioria das pessoas comete o mesmo erro aqui:";
            }
            return string.Join("\n\n", lines);
        }

        if (lowerInst.Contains("pergunta") || lowerInst.Contains("reflex"))
        {
            return $"{text}\n\nConcorda ou sua experiência em produção foi diferente?";
        }

        if (lowerInst.Contains("número") || lowerInst.Contains("dado") || lowerInst.Contains("métrica"))
        {
            return $"{text}\n\nOs dados não mentem: 80% do ganho de produtividade veio de cortar o que não importava.";
        }

        // Adiciona nota de refinamento genérica
        return $"{text}\n\n[Refinado: {instruction}]";
    }

    private async Task<List<PostDraft>?> TryGenerateWithLlmAsync(string apiKey, List<MemoryItem> activeMemories, VoiceProfile voiceProfile, AppSettings settings)
    {
        string prompt = BuildSystemPrompt(activeMemories, voiceProfile);
        string userPrompt = "Gere 2 temas de conteúdo social com versão para o X (máximo 280 caracteres) e LinkedIn (estruturado com quebras verticais). Retorne em formato JSON válido.";

        if (settings.AiProvider.Contains("OpenAI", StringComparison.OrdinalIgnoreCase) || settings.AiProvider.Contains("Groq", StringComparison.OrdinalIgnoreCase))
        {
            var reqObj = new JsonObject
            {
                ["model"] = string.IsNullOrWhiteSpace(settings.AiModel) ? "gpt-4o-mini" : settings.AiModel,
                ["messages"] = new JsonArray
                {
                    new JsonObject { ["role"] = "system", ["content"] = prompt },
                    new JsonObject { ["role"] = "user", ["content"] = userPrompt }
                },
                ["response_format"] = new JsonObject { ["type"] = "json_object" },
                ["temperature"] = 0.7
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Content = new StringContent(reqObj.ToJsonString(), Encoding.UTF8, "application/json");

            var res = await _httpClient.SendAsync(request);
            if (res.IsSuccessStatusCode)
            {
                string jsonRes = await res.Content.ReadAsStringAsync();
                return ParseLlmResponse(jsonRes);
            }
        }

        return null;
    }

    private async Task<string?> TryRefineWithLlmAsync(string apiKey, PostDraft draft, string instruction, List<MemoryItem> activeMemories, AppSettings settings)
    {
        if (settings.AiProvider.Contains("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            var reqObj = new JsonObject
            {
                ["model"] = string.IsNullOrWhiteSpace(settings.AiModel) ? "gpt-4o-mini" : settings.AiModel,
                ["messages"] = new JsonArray
                {
                    new JsonObject { ["role"] = "system", ["content"] = "Você é o assistente Uttu. Refine o texto para a rede social informada seguindo rigorosamente a instrução. Retorne apenas o texto refinado sem aspas ou explicações." },
                    new JsonObject { ["role"] = "user", ["content"] = $"Rede: {draft.Platform}\nTexto Original:\n{draft.Content}\n\nInstrução: {instruction}" }
                },
                ["temperature"] = 0.5
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Content = new StringContent(reqObj.ToJsonString(), Encoding.UTF8, "application/json");

            var res = await _httpClient.SendAsync(request);
            if (res.IsSuccessStatusCode)
            {
                string jsonRes = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonRes);
                var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                return content;
            }
        }

        return null;
    }

    private string BuildSystemPrompt(List<MemoryItem> memories, VoiceProfile profile)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Você é o Uttu, copiloto nativo de conteúdo social.");
        sb.AppendLine("Sua missão é gerar ideias de posts altamente pragmáticos, focados em engenharia de software e produtos digitais.");
        sb.AppendLine();
        sb.AppendLine("DIRETRIZES DE ESTILO E VOZ:");
        sb.AppendLine($"- Tamanho médio de sentença desejado: {profile.AvgSentenceLength} palavras.");
        sb.AppendLine($"- Estilo de gancho: {profile.HookStyle}.");
        sb.AppendLine($"- Tom predominante: {profile.ToneKeywords}.");
        sb.AppendLine($"- Palavras e clichês estritamente proibidos: {profile.ProhibitedWords}.");
        sb.AppendLine();
        sb.AppendLine("MEMÓRIAS ATIVAS DO USUÁRIO:");
        foreach (var m in memories.Where(x => x.IsActive))
        {
            sb.AppendLine($"- [{m.Category}] {m.Text}");
        }
        sb.AppendLine();
        sb.AppendLine("FORMATO DE RESPOSTA (JSON):");
        sb.AppendLine("""
        {
            "drafts": [
                { "theme": "...", "platform": "X", "content": "..." },
                { "theme": "...", "platform": "LinkedIn", "content": "..." }
            ]
        }
        """);

        return sb.ToString();
    }

    private List<PostDraft>? ParseLlmResponse(string jsonResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var contentStr = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrEmpty(contentStr)) return null;

            using var innerDoc = JsonDocument.Parse(contentStr);
            if (innerDoc.RootElement.TryGetProperty("drafts", out var draftsArr))
            {
                var list = new List<PostDraft>();
                foreach (var d in draftsArr.EnumerateArray())
                {
                    list.Add(new PostDraft
                    {
                        Theme = d.GetProperty("theme").GetString() ?? "Tema",
                        Platform = d.GetProperty("platform").GetString() ?? "X",
                        Content = d.GetProperty("content").GetString() ?? "",
                        Status = "Draft",
                        CreatedAt = DateTime.Now
                    });
                }
                return list;
            }
        }
        catch { }
        return null;
    }
}
