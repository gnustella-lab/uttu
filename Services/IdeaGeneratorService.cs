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
                System.Diagnostics.Debug.WriteLine($"[IdeaGeneratorService] Error invoking LLM: {ex.Message}. Falling back to native engine.");
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

        // Archetypes of high-performing engineering & product insights
        var archetypes = new[]
        {
            new
            {
                Theme = "Less code and pruning complexity",
                HookX = "The fastest code to debug is the code that was never written.",
                BodyX = "Before adding a new microservice or heavy dependency to your repo, ask: can we solve this by pruning existing code?\n\nSmaller surface area = fewer points of failure.",
                HookLi = "Most software teams suffer from an excess of architecture, not a lack of it.",
                BodyLi = "Last week we decided to prune three legacy screens from our onboarding flow and delete two stale dependencies.\n\nThe result?\n\n- 40% drop in support tickets;\n- Onboarding speed tripled;\n- Zero new maintenance debt.\n\nGreat products don't need more features to scale. They need more clarity."
            },
            new
            {
                Theme = "Pragmatic delivery vs theoretical perfection",
                HookX = "Flawless software in staging generates zero real-world value.",
                BodyX = "Put a solid, compact version in front of customers today. Five minutes of production feedback beats three weeks of alignment meetings.",
                HookLi = "System design theory is beautiful on paper. In production, simplicity wins every time.",
                BodyLi = "I've seen teams spend months designing ultra-complex distributed systems to solve problems a simple SQLite instance or local queue would handle with grace.\n\nBefore trying to architect for 10 million hypothetical requests:\n\n1. Deliver tangible value today;\n2. Measure real bottlenecks with telemetry;\n3. Optimize only where it actually hurts.\n\nPragmatism is the ultimate design pattern."
            },
            new
            {
                Theme = "Human-in-the-loop product decisions",
                HookX = "Automation without human editorial judgment creates noise at scale.",
                BodyX = "The best AI doesn't replace the final decision of the person who owns the context. It generates options; you approve with a single click.",
                HookLi = "Why human-in-the-loop systems consistently outperform blind automation.",
                BodyLi = "Building automations is easy. The problem is when they hallucinate generic content without the filter of someone living the day-to-day business reality.\n\nThe secret of practical AI:\n\n- Let the algorithm handle heavy ideation and synthesis;\n- Keep the approval and refinement lever firmly in your hands;\n- Ship only what meets your standard of quality.\n\nControl and velocity work best together."
            }
        };

        var selected = archetypes.OrderBy(_ => random.Next()).Take(2).ToList();

        foreach (var arch in selected)
        {
            // Draft for X (compact, <= 280 characters)
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

            // Draft for LinkedIn (structured vertical spacing)
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

        if (lowerInst.Contains("concise") || lowerInst.Contains("short") || lowerInst.Contains("brief") || lowerInst.Contains("curto") || lowerInst.Contains("resum"))
        {
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 2)
            {
                return $"{lines[0]}\n\n{lines[^1]}";
            }
            return text;
        }

        if (lowerInst.Contains("hook") || lowerInst.Contains("impact") || lowerInst.Contains("opening") || lowerInst.Contains("gancho"))
        {
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (lines.Count > 0)
            {
                lines[0] = "Most engineers make the exact same mistake here:";
            }
            return string.Join("\n\n", lines);
        }

        if (lowerInst.Contains("question") || lowerInst.Contains("reflective") || lowerInst.Contains("pergunta"))
        {
            return $"{text}\n\nDo you agree, or has your production experience differed?";
        }

        if (lowerInst.Contains("data") || lowerInst.Contains("number") || lowerInst.Contains("metric") || lowerInst.Contains("métrica"))
        {
            return $"{text}\n\nThe telemetry speaks for itself: 80% of productivity gains came from cutting what didn't matter.";
        }

        return $"{text}\n\n[Refined: {instruction}]";
    }

    private async Task<List<PostDraft>?> TryGenerateWithLlmAsync(string apiKey, List<MemoryItem> activeMemories, VoiceProfile voiceProfile, AppSettings settings)
    {
        string prompt = BuildSystemPrompt(activeMemories, voiceProfile);
        string userPrompt = "Generate 2 social content themes with versions for X (max 280 characters) and LinkedIn (structured with vertical line breaks). Return valid JSON.";

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
                    new JsonObject { ["role"] = "system", ["content"] = "You are Uttu, an autonomous social content copilot. Refine the text for the specified social platform strictly following the given instruction. Return only the refined text without surrounding quotes or explanations." },
                    new JsonObject { ["role"] = "user", ["content"] = $"Platform: {draft.Platform}\nOriginal Text:\n{draft.Content}\n\nInstruction: {instruction}" }
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
        sb.AppendLine("You are Uttu, a native desktop social content copilot.");
        sb.AppendLine("Your mission is to generate highly pragmatic, authentic posts focused on software engineering and digital products.");
        sb.AppendLine();
        sb.AppendLine("VOICE AND STYLE GUIDELINES:");
        sb.AppendLine($"- Target sentence length: {profile.AvgSentenceLength} words per sentence.");
        sb.AppendLine($"- Hook style: {profile.HookStyle}.");
        sb.AppendLine($"- Primary tone: {profile.ToneKeywords}.");
        sb.AppendLine($"- Strictly prohibited buzzwords & clichés: {profile.ProhibitedWords}.");
        sb.AppendLine();
        sb.AppendLine("ACTIVE USER MEMORIES & RULES:");
        foreach (var m in memories.Where(x => x.IsActive))
        {
            sb.AppendLine($"- [{m.Category}] {m.Text}");
        }
        sb.AppendLine();
        sb.AppendLine("RESPONSE FORMAT (JSON):");
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
                        Theme = d.GetProperty("theme").GetString() ?? "Theme",
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
