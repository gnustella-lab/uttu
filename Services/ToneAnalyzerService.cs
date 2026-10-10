using System.Text.RegularExpressions;
using Uttu.Models;

namespace Uttu.Services;

public class ToneAnalyzerService
{
    private static readonly string[] ClichéList = new[]
    {
        "synergy", "disruptive", "revolutionary", "game changer", "leverage",
        "mindset", "paradigm", "holistic", "innovative ecosystem", "360 alignment",
        "outside the box", "deep dive", "quick win"
    };

    public VoiceProfile AnalyzeSamples(string rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return new VoiceProfile();
        }

        // Split by post delimiters or paragraphs
        var posts = rawInput.Split(new[] { "---", "===", "\n\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(p => p.Trim())
                            .Where(p => p.Length > 20)
                            .ToList();

        if (posts.Count == 0)
        {
            posts.Add(rawInput.Trim());
        }

        var allSentences = new List<string>();
        var allWords = new List<string>();
        int totalPunctuation = 0;

        foreach (var post in posts)
        {
            var punctMatches = Regex.Matches(post, @"[.,;:!?—–\-\(\)]");
            totalPunctuation += punctMatches.Count;

            var sentences = Regex.Split(post, @"(?<=[.!?])\s+")
                                 .Select(s => s.Trim())
                                 .Where(s => s.Length > 0);
            allSentences.AddRange(sentences);

            var words = Regex.Split(post, @"\W+")
                             .Where(w => w.Length > 0);
            allWords.AddRange(words);
        }

        int wordCount = Math.Max(1, allWords.Count);
        int sentenceCount = Math.Max(1, allSentences.Count);

        double avgSentenceLength = Math.Round((double)wordCount / sentenceCount, 1);
        double punctuationDensity = Math.Round((double)totalPunctuation / wordCount * 100, 1);

        string hookStyle = DetectHookStyle(posts);

        var prohibitedFound = ClichéList
            .Where(c => rawInput.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        string prohibitedList = prohibitedFound.Count > 0 
            ? string.Join(", ", prohibitedFound) 
            : "synergy, disruptive, revolutionary, game changer, leverage, mindset";

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

            if (Regex.IsMatch(firstLine, @"^(Don't|Stop|Most|Never|Forget|Fewer|Less|No|Nem|Não)\b", RegexOptions.IgnoreCase))
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
            return "Contrarian / Provocative (Breaks common assumption in 1st line)";
        if (directAssertionCount >= 1)
            return "Short Direct Assertion (High-clarity synthetic hook)";
        if (numericCount >= 1)
            return "Metrics-driven / Numeric (Data points and compact lists)";
        if (questionCount >= 1)
            return "Reflective Question (Conceptual challenge to the reader)";

        return "Pragmatic & Concise (Product and code focus)";
    }

    private string DetectToneKeywords(double avgSentenceLen, double puncDensity, List<string> posts)
    {
        var tones = new List<string>();

        if (avgSentenceLen < 12)
            tones.Add("Extremely Concise");
        else if (avgSentenceLen < 18)
            tones.Add("Pragmatic & Direct");
        else
            tones.Add("Reflective Essay");

        if (puncDensity > 8.0)
            tones.Add("Cadenced Rhythm");
        else
            tones.Add("Linear Flow");

        tones.Add("Software Engineering");
        tones.Add("Zero Fluff");
        tones.Add("Product-driven");

        return string.Join(", ", tones);
    }
}
