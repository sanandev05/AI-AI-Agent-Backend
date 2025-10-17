using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AI_AI_Agent.Infrastructure.Tools;

/// <summary>
/// Performs OCR on an image (local path or URL) using an external OCR provider.
/// Currently supports OCR.Space out of the box; other providers can be added via configuration.
/// </summary>
public sealed class ImageTextExtractTool : ITool
{
    public string Name => "ImageTextExtract";
    public string Description => "Extract text from an image using OCR (requires OCR provider API key).";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = "Local image path or HTTP/HTTPS URL" },
            language = new { type = "string", description = "OCR language code (default 'eng')" }
        },
        required = new[] { "path" }
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ImageTextExtractTool> _logger;
    private static readonly string[] SupportedExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".gif", ".webp" };

    public ImageTextExtractTool(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<ImageTextExtractTool> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
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

        var language = args.TryGetProperty("language", out var langProp) && langProp.ValueKind == JsonValueKind.String
            ? (langProp.GetString() ?? "eng")
            : "eng";

        try
        {
            var localPath = await EnsureLocalFileAsync(input, ct);
            if (localPath is null)
            {
                return new { success = false, error = "Unable to access file" };
            }

            if (!SupportedExtensions.Contains(Path.GetExtension(localPath).ToLowerInvariant()))
            {
                return new { success = false, error = "Unsupported image format" };
            }

            var provider = _configuration["Ocr:Provider"] ?? "ocrspace";
            var apiKey = _configuration["Ocr:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new
                {
                    success = false,
                    error = "OCR provider API key not configured (set Ocr:ApiKey in appsettings or user secrets)",
                    provider
                };
            }

            var endpoint = _configuration["Ocr:Endpoint"] ?? "https://api.ocr.space/parse/image";
            var text = await CallOcrSpaceAsync(endpoint, apiKey!, localPath, language, ct);

            if (text.Length == 0)
            {
                return new { success = false, error = "OCR returned empty result", provider };
            }

            var preview = text.Length > 400 ? text.Substring(0, 400) + "..." : text;
            return new
            {
                success = true,
                provider,
                language,
                wordCount = CountWords(text),
                characterCount = text.Length,
                preview,
                text
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR failed for {Path}", input);
            return new { success = false, error = ex.Message };
        }
    }

    private async Task<string?> EnsureLocalFileAsync(string input, CancellationToken ct)
    {
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(input, ct);
            response.EnsureSuccessStatusCode();
            var extension = Path.GetExtension(new Uri(input).AbsolutePath);
            if (string.IsNullOrWhiteSpace(extension)) extension = ".png";
            var tempPath = Path.Combine(Path.GetTempPath(), $"ocr_{Guid.NewGuid():N}{extension}");
            await using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs, ct);
            return tempPath;
        }

        if (!File.Exists(input)) return null;
        return input;
    }

    private async Task<string> CallOcrSpaceAsync(string endpoint, string apiKey, string filePath, string language, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(apiKey), "apikey");
        content.Add(new StringContent(language), "language");
        content.Add(new StringContent("true"), "isOverlayRequired");
        var bytes = await File.ReadAllBytesAsync(filePath, ct);
        var byteContent = new ByteArrayContent(bytes);
        byteContent.Headers.Add("Content-Type", "application/octet-stream");
        content.Add(byteContent, "file", Path.GetFileName(filePath));

        using var response = await client.PostAsync(endpoint, content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"OCR request failed: {response.StatusCode} - {error}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (doc.RootElement.TryGetProperty("IsErroredOnProcessing", out var errored) && errored.GetBoolean())
        {
            var message = doc.RootElement.TryGetProperty("ErrorMessage", out var errMsg) && errMsg.ValueKind == JsonValueKind.Array
                ? string.Join(";", errMsg.EnumerateArray().Select(x => x.GetString()))
                : "Unknown OCR error";
            throw new InvalidOperationException(message);
        }

        if (!doc.RootElement.TryGetProperty("ParsedResults", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        double? confidence = null;
        foreach (var result in results.EnumerateArray())
        {
            if (result.TryGetProperty("ParsedText", out var textProp) && textProp.ValueKind == JsonValueKind.String)
            {
                sb.AppendLine(textProp.GetString());
            }
            if (result.TryGetProperty("MeanConfidence", out var meanConf) && meanConf.ValueKind == JsonValueKind.Number)
            {
                confidence = meanConf.GetDouble();
            }
        }

        if (confidence.HasValue)
        {
            _logger.LogInformation("OCR confidence: {Confidence}", confidence);
        }

        return sb.ToString().Trim();
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var parts = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length;
    }
}
