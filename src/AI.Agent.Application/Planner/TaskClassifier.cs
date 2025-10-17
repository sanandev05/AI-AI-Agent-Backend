using System.Text.RegularExpressions;

namespace AI.Agent.Application.Planner;

public record TaskClassification(bool needsWeb, bool needsCode, bool needsFiles, string category);

public static class TaskClassifier
{
    private static readonly Regex ArithmeticRegex = new(@"^[\n\r\t ]*[\d\s\(\)\+\-\*\/\.\^%]+$", RegexOptions.Compiled);

    public static TaskClassification Classify(string goal)
    {
        var text = goal ?? string.Empty;
        var lower = text.ToLowerInvariant();

        // Heuristic shortcut: arithmetic
        if (ArithmeticRegex.IsMatch(text))
        {
            return new TaskClassification(needsWeb: false, needsCode: false, needsFiles: false, category: "arithmetic");
        }

        var webHints = new[] { "find", "latest", "official", "website", "google", "bing", "search", "browse", "visit", "navigate", "from vendor", "from site", "link", "url", "http" };
        var codeHints = new[] { "write code", "implement", "function", "algorithm", "bug", "compile", "unit test" };
        var fileHints = new[] { "docx", "pdf", "pptx", "generate document", "create file", "download" };

        var needsWeb = webHints.Any(h => lower.Contains(h)) || ContainsUrl(text);
        var needsCode = codeHints.Any(h => lower.Contains(h));
        var needsFiles = fileHints.Any(h => lower.Contains(h));

        var category = needsWeb ? "web-research" : needsCode ? "code" : needsFiles ? "files" : "general-qa";
        return new TaskClassification(needsWeb, needsCode, needsFiles, category);
    }

    private static bool ContainsUrl(string text)
    {
        return Regex.IsMatch(text, @"https?://[^\s<>""\r\n]+", RegexOptions.IgnoreCase);
    }
}
