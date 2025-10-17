using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;

namespace AI_AI_Agent.Infrastructure.Tools;

public sealed class ExtractorTool : ITool
{
    public string Name => "Extractor";
    public string Description => "Extracts structured financial data {value, unit, period, sourceUrl} from text using regex first, LLM backup if needed.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            text = new { type = "string", description = "Raw text to extract data from" },
            sourceUrl = new { type = "string", description = "Source URL for citation" },
            metric = new { type = "string", description = "Target metric (e.g., 'revenue', 'margin')" }
        },
        required = new[] { "text", "sourceUrl" }
    };

    private readonly Kernel _kernel;
    private readonly ILogger<ExtractorTool> _logger;

    public ExtractorTool(Kernel kernel, ILogger<ExtractorTool> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        var text = args.GetProperty("text").GetString() ?? "";
        var sourceUrl = args.GetProperty("sourceUrl").GetString() ?? "";
        var metric = args.TryGetProperty("metric", out var m) ? m.GetString() ?? "revenue" : "revenue";

        if (string.IsNullOrWhiteSpace(text))
            return new { error = "Text is required", success = false };

        // Step 1: Try regex extraction first
        var regexResults = ExtractWithRegex(text, sourceUrl, metric);
        if (regexResults.Any())
        {
            _logger.LogInformation("Regex extraction successful, found {Count} matches", regexResults.Count);
            return new { 
                success = true, 
                method = "regex", 
                results = regexResults.OrderByDescending(r => r.Confidence).ToList(),
                sourceUrl,
                message = "Data extracted using regex patterns"
            };
        }

        // Step 2: Fallback to LLM extraction
        _logger.LogWarning("Regex extraction failed, falling back to LLM");
        try
        {
            var llmResult = await ExtractWithLLM(text, sourceUrl, metric, ct);
            return new { 
                success = true, 
                method = "llm_backup", 
                results = new[] { llmResult },
                sourceUrl,
                message = "Data extracted using LLM backup"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Both regex and LLM extraction failed");
            return new { 
                error = $"Extraction failed: {ex.Message}", 
                success = false,
                method = "both_failed"
            };
        }
    }

    private List<FinancialData> ExtractWithRegex(string text, string sourceUrl, string targetMetric)
    {
        var results = new List<FinancialData>();
        
        // Common financial value patterns with units
        var valuePatterns = new[]
        {
            // $XX.X billion, $XX.X million, etc.
            @"\$\s*(\d{1,3}(?:[,\s]?\d{3})*(?:\.\d+)?)\s*(billion|million|bn|mn|b|m)(?:\s*(USD|dollars?|US\$)?)",
            
            // XX.X billion USD, etc.
            @"(\d{1,3}(?:[,\s]?\d{3})*(?:\.\d+)?)\s*(billion|million|bn|mn|b|m)\s*(USD|US\$|dollars?)",
            
            // Revenue: $XX.X billion format
            @"(?:revenue|sales|income|earnings?):\s*\$?\s*(\d{1,3}(?:[,\s]?\d{3})*(?:\.\d+)?)\s*(billion|million|bn|mn|b|m)",
            
            // Standalone large numbers near revenue keywords
            @"(?:revenue|sales|income|earnings?)[^\d]*(\d{1,3}(?:[,\s]?\d{3})*(?:\.\d+)?)\s*(billion|million|bn|mn|b|m)?"
        };

        // Period patterns
        var periodPatterns = new[]
        {
            @"FY\s*(\d{4})",                    // FY2023
            @"(?:fiscal|financial)\s+year\s+(\d{4})", // fiscal year 2023
            @"Q([1-4])\s+(\d{4})",              // Q1 2023
            @"(\d{4})\s+Q([1-4])",              // 2023 Q1
            @"(?:quarter|q)\s*([1-4])[,\s]*(\d{4})", // Quarter 1, 2023
            @"year\s+ended?\s+\w+\s+\d{1,2},?\s+(\d{4})", // year ended December 31, 2023
        };

        foreach (var valuePattern in valuePatterns)
        {
            var valueMatches = Regex.Matches(text, valuePattern, RegexOptions.IgnoreCase);
            foreach (Match valueMatch in valueMatches)
            {
                if (!valueMatch.Success) continue;

                var valueStr = valueMatch.Groups[1].Value.Replace(",", "").Replace(" ", "");
                if (!decimal.TryParse(valueStr, out var value)) continue;

                var unit = NormalizeUnit(valueMatch.Groups[2].Value);
                var normalizedValue = NormalizeValue(value, unit);
                
                // Look for period information near this match
                var contextStart = Math.Max(0, valueMatch.Index - 200);
                var contextEnd = Math.Min(text.Length, valueMatch.Index + valueMatch.Length + 200);
                var context = text.Substring(contextStart, contextEnd - contextStart);
                
                var period = ExtractPeriod(context, periodPatterns);
                
                var confidence = CalculateConfidence(context, targetMetric, valueMatch.Value);
                
                results.Add(new FinancialData
                {
                    Metric = targetMetric,
                    Value = normalizedValue,
                    Unit = "USD",
                    Period = period ?? "Unknown",
                    SourceUrl = sourceUrl,
                    Context = valueMatch.Value,
                    Confidence = confidence
                });
            }
        }

        return results.Where(r => r.Confidence > 0.3).ToList();
    }

    private string? ExtractPeriod(string context, string[] periodPatterns)
    {
        foreach (var pattern in periodPatterns)
        {
            var match = Regex.Match(context, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (match.Groups.Count == 2)
                {
                    return $"FY{match.Groups[1].Value}";
                }
                else if (match.Groups.Count == 3)
                {
                    return $"Q{match.Groups[1].Value} {match.Groups[2].Value}";
                }
            }
        }
        return null;
    }

    private decimal NormalizeValue(decimal value, string unit)
    {
        return unit.ToLowerInvariant() switch
        {
            "billion" or "bn" or "b" => value * 1_000_000_000,
            "million" or "mn" or "m" => value * 1_000_000,
            _ => value
        };
    }

    private string NormalizeUnit(string unit)
    {
        return unit.ToLowerInvariant() switch
        {
            "billion" or "bn" or "b" => "billion",
            "million" or "mn" or "m" => "million",
            _ => unit.ToLowerInvariant()
        };
    }

    private double CalculateConfidence(string context, string targetMetric, string matchedValue)
    {
        var score = 0.5; // Base score for any numerical match
        
        var lowerContext = context.ToLowerInvariant();
        var lowerMetric = targetMetric.ToLowerInvariant();
        
        // Boost if target metric appears nearby
        if (lowerContext.Contains(lowerMetric) || 
            (lowerMetric == "revenue" && (lowerContext.Contains("sales") || lowerContext.Contains("income"))))
        {
            score += 0.3;
        }
        
        // Boost for financial keywords
        var financialKeywords = new[] { "revenue", "sales", "income", "earnings", "profit", "loss", "margin" };
        foreach (var keyword in financialKeywords)
        {
            if (lowerContext.Contains(keyword)) score += 0.1;
        }
        
        // Boost for period indicators
        if (Regex.IsMatch(context, @"(?:FY|Q[1-4]|\d{4})", RegexOptions.IgnoreCase))
        {
            score += 0.2;
        }
        
        // Boost for currency symbols
        if (matchedValue.Contains("$") || lowerContext.Contains("usd") || lowerContext.Contains("dollar"))
        {
            score += 0.1;
        }
        
        return Math.Min(1.0, score);
    }

    private async Task<FinancialData> ExtractWithLLM(string text, string sourceUrl, string metric, CancellationToken ct)
    {
        var prompt = $@"From the provided text, extract financial data and return valid JSON with this exact structure:
{{
  ""metric"": ""{metric}"",
  ""value"": number,
  ""unit"": ""USD"",
  ""period"": ""FYyyyy or Qn yyyy"",
  ""source_url"": ""{sourceUrl}""
}}

Rules:
- Convert all values to USD if not already
- Normalize periods to FY2023 or Q1 2023 format
- If multiple candidates exist, choose the most recent period
- Return ONLY the JSON, no explanation

Text to analyze:
{text.Substring(0, Math.Min(text.Length, 2000))}";

        var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: ct);
        var jsonStr = result.GetValue<string>() ?? "{}";
        
        // Parse LLM response
        try
        {
            var llmData = JsonSerializer.Deserialize<JsonElement>(jsonStr);
            
            return new FinancialData
            {
                Metric = llmData.TryGetProperty("metric", out var m) ? m.GetString() ?? metric : metric,
                Value = llmData.TryGetProperty("value", out var v) ? v.GetDecimal() : 0,
                Unit = llmData.TryGetProperty("unit", out var u) ? u.GetString() ?? "USD" : "USD",
                Period = llmData.TryGetProperty("period", out var p) ? p.GetString() ?? "Unknown" : "Unknown",
                SourceUrl = sourceUrl,
                Context = jsonStr,
                Confidence = 0.7 // LLM backup gets moderate confidence
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse LLM response: {Response}", jsonStr);
            throw new InvalidOperationException($"LLM returned invalid JSON: {jsonStr}");
        }
    }

    private class FinancialData
    {
        public string Metric { get; set; } = "";
        public decimal Value { get; set; }
        public string Unit { get; set; } = "";
        public string Period { get; set; } = "";
        public string SourceUrl { get; set; } = "";
        public string Context { get; set; } = "";
        public double Confidence { get; set; }
    }
}