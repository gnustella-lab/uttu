using System.Text.RegularExpressions;
using Uttu.Models;

namespace Uttu.Services;

public class ToneAnalyzerService
{
    private static readonly string[] ClichéList = new[]
    {
        "sinergia", "disruptivo", "revolucionário", "game changer", "alavancar",
        "mindset", "paradigma", "holístico", "ecossistema inovador", "resiliência 360",
        "fora da caixa", "mergulho profundo", "quick win"
    };

    public VoiceProfile AnalyzeSamples(string rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return new VoiceProfile();
        }

        // Divide por delimitadores ou parágrafos
        var posts = rawInput.Split(new[] { "---", "===", "\n\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(p => p.Trim())
                            .Where(p => p.Length > 20)
                            .ToList();

        if (posts.Count == 0)
        {
            posts.Add(rawInput.Trim());
        }

        // Sentenças e palavras
        var allSentences = new List<string>();
        var allWords = new List<string>();
        int totalPunctuation = 0;

        foreach (var post in posts)
        {
            // Punctuation count: . , ; : ! ? - em-dash
            var punctMatches = Regex.Matches(post, @"[.,;:!?—–\-\(\)]");
            totalPunctuation += punctMatches.Count;

            // Sentenças
            var sentences = Regex.Split(post, @"(?<=[.!?])\s+")
                                 .Select(s => s.Trim())
                                 .Where(s => s.Length > 0);
            allSentences.AddRange(sentences);

            // Palavras
            var words = Regex.Split(post, @"\W+")
                             .Where(w => w.Length > 0);
            allWords.AddRange(words);
        }

        int wordCount = Math.Max(1, allWords.Count);
        int sentenceCount = Math.Max(1, allSentences.Count);

        double avgSentenceLength = Math.Round((double)wordCount / sentenceCount, 1);
        double punctuationDensity = Math.Round((double)totalPunctuation / wordCount * 100, 1);

        // Análise de Ganchos (como os posts iniciam)
        string hookStyle = DetectHookStyle(posts);

        // Detecção de Clichês encontrados para marcar como proibidos
        var prohibitedFound = ClichéList
            .Where(c => rawInput.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        string prohibitedList = prohibitedFound.Count > 0 
            ? string.Join(", ", prohibitedFound) 
            : "sinergia, disruptivo, revolucionário, game changer, alavancar, mindset";

        // Detecção de tom dominante
        string toneKeywords = DetectToneKeywords(avgSentenceLength, punctuationDensity, posts);

        return new VoiceProfile
        {
            SampleText = rawInput,
            AvgSentenceLength = avgSentenceLength,
            PunctuationDensity = punctuationDensity,
            ProhibitedWords = prohibitedList,
            HookStyle = hookStyle,
            ToneKeywords = toneKeywords,
            AnalyzedAt = DateTime.Now
        };
    }

    private string DetectHookStyle(List<string> posts)
    {
        int contrarianCount = 0;
        int directAssertionCount = 0;
        int questionCount = 0;
        int numericCount = 0;

        foreach (var post in posts)
        {
            string firstLine = post.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            firstLine = firstLine.Trim();

            if (Regex.IsMatch(firstLine, @"^(Não|Nem|Pare|Esqueça|O maior erro|A maioria|Menos|Nenhum)\b", RegexOptions.IgnoreCase))
            {
                contrarianCount++;
            }
            else if (Regex.IsMatch(firstLine, @"^\d+\b|^\d+%", RegexOptions.IgnoreCase))
            {
                numericCount++;
            }
            else if (firstLine.EndsWith("?"))
            {
                questionCount++;
            }
            else if (firstLine.Split(' ').Length <= 8)
            {
                directAssertionCount++;
            }
        }

        if (contrarianCount >= 1)
            return "Contrário / Provocativo (Quebra de crença comum na 1ª linha)";
        if (directAssertionCount >= 1)
            return "Afirmação Direta e Curta (Gancho sintético de alta clareza)";
        if (numericCount >= 1)
            return "Baseado em Métricas / Numérico (Dados e listas compactas)";
        if (questionCount >= 1)
            return "Pergunta Reflexiva (Desafio conceitual ao leitor)";

        return "Pragmático e Condensado (Foco em produto e código)";
    }

    private string DetectToneKeywords(double avgSentenceLen, double puncDensity, List<string> posts)
    {
        var tones = new List<string>();

        if (avgSentenceLen < 12)
            tones.Add("Extremamente Conciso");
        else if (avgSentenceLen < 18)
            tones.Add("Pragmático e Direto");
        else
            tones.Add("Ensaístico e Reflexivo");

        if (puncDensity > 8.0)
            tones.Add("Ritmo Cadenciado");
        else
            tones.Add("Fluxo Linear");

        tones.Add("Engenharia de Software");
        tones.Add("Zero Enrolação");
        tones.Add("Foco em Produto");

        return string.Join(", ", tones);
    }
}
