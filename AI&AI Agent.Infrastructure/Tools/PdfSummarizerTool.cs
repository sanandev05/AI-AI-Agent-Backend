using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.Extensions.Logging;

namespace AI_AI_Agent.Infrastructure.Tools;

/// <summary>
/// Extracts text from a PDF (local file or URL) and returns a concise extractive summary with key phrases.
/// </summary>
public sealed class PdfSummarizerTool : ITool
{
    public string Name => "PdfSummarize";
    public string Description => "Summarize a PDF by extracting text and returning key sentences, keywords, and metadata.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = "Local path or HTTP/HTTPS URL to the PDF" },
            maxPages = new { type = "number", description = "Maximum pages to read (default 10)" },
            maxSummarySentences = new { type = "number", description = "Maximum sentences in summary (default 6)" }
        },
        required = new[] { "path" }
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PdfSummarizerTool> _logger;

    public PdfSummarizerTool(IHttpClientFactory httpClientFactory, ILogger<PdfSummarizerTool> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("path", out var pathProp) || pathProp.ValueKind != JsonValueKind.String)
        {
            return new { success = false, error = "path is required" };
        }

        var input = pathProp.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return new { success = false, error = "path cannot be empty" };
        }

        int maxPages = args.TryGetProperty("maxPages", out var mpProp) && mpProp.ValueKind == JsonValueKind.Number ? mpProp.GetInt32() : 10;
        int maxSummarySentences = args.TryGetProperty("maxSummarySentences", out var msProp) && msProp.ValueKind == JsonValueKind.Number ? msProp.GetInt32() : 6;

        try
        {
            var localPath = await EnsureLocalPdfAsync(input, ct);
            if (localPath is null || !File.Exists(localPath))
            {
                return new { success = false, error = "Unable to access PDF" };
            }

            var text = ExtractText(localPath, maxPages);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new { success = false, error = "No text extracted from PDF" };
            }

            var sentences = SplitSentences(text).ToList();
            var keywords = ExtractKeywords(text, 12);
            var summary = BuildSummary(sentences, keywords, maxSummarySentences);
            var preview = text.Length > 500 ? text.Substring(0, 500) + "..." : text;

            return new
            {
                success = true,
                path = localPath,
                pageCount = CountPages(localPath),
                extractedCharacters = text.Length,
                preview,
                summary,
                summarySentenceCount = summary.Count,
                keywords,
                sources = new[] { input }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF summarization failed for {Path}", input);
            return new { success = false, error = ex.Message };
        }
    }

    private async Task<string?> EnsureLocalPdfAsync(string input, CancellationToken ct)
    {
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(input, ct);
            response.EnsureSuccessStatusCode();
            var tempFile = Path.Combine(Path.GetTempPath(), $"pdf_{Guid.NewGuid():N}.pdf");
            await using var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs, ct);
            return tempFile;
        }

        return File.Exists(input) ? input : null;
    }

    private static string ExtractText(string pdfPath, int maxPages)
    {
        var sb = new StringBuilder();
        using var pdf = new PdfDocument(new PdfReader(pdfPath));
        int totalPages = pdf.GetNumberOfPages();
        int limit = Math.Min(Math.Max(1, maxPages), totalPages);
        for (int i = 1; i <= limit; i++)
        {
            var strategy = new SimpleTextExtractionStrategy();
            var pageText = PdfTextExtractor.GetTextFromPage(pdf.GetPage(i), strategy);
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                sb.AppendLine(pageText.Trim());
            }
        }
        return sb.ToString().Trim();
    }

    private static int CountPages(string pdfPath)
    {
        using var pdf = new PdfDocument(new PdfReader(pdfPath));
        return pdf.GetNumberOfPages();
    }

    private static IEnumerable<string> SplitSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        var sentences = Regex.Split(text, "(?<=[.!?])\\s+");
        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            if (trimmed.Length > 0)
            {
                yield return trimmed;
            }
        }
    }

    private static List<string> ExtractKeywords(string text, int top)
    {
        var stopWords = new HashSet<string>(new[]
        {
            "the","and","that","have","for","not","with","you","this","but","his","from","they","she","which","would","there","their","about","could","into","than","them","these","because","other","were","your","been"
        }, StringComparer.OrdinalIgnoreCase);

        var wordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var matches = Regex.Matches(text, "[A-Za-z]{4,}");
        foreach (Match match in matches)
        {
            var word = match.Value.ToLowerInvariant();
            if (stopWords.Contains(word)) continue;
            if (!wordCounts.TryAdd(word, 1)) wordCounts[word]++;
        }

        return wordCounts.OrderByDescending(kv => kv.Value).Take(top).Select(kv => kv.Key).ToList();
    }

    private static List<string> BuildSummary(List<string> sentences, List<string> keywords, int maxSentences)
    {
        if (sentences.Count == 0) return new List<string>();

        var keywordSet = new HashSet<string>(keywords, StringComparer.OrdinalIgnoreCase);
        var scores = new List<(string sentence, double score)>();
        foreach (var sentence in sentences)
        {
            double score = 0;
            var words = Regex.Matches(sentence, "[A-Za-z]{4,}").Select(m => m.Value.ToLowerInvariant());
            foreach (var word in words)
            {
                if (keywordSet.Contains(word))
                {
                    score += 1.0;
                }
            }
            score += Math.Min(sentence.Length / 200.0, 2.0); // prefer longer informative sentences
            scores.Add((sentence, score));
        }

        return scores
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => x.sentence.Length)
            .Take(Math.Max(1, maxSentences))
            .Select(x => x.sentence)
            .ToList();
    }
}
