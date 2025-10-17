using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AI_AI_Agent.Infrastructure.Tools;

/// <summary>
/// Transcribes audio files to text using the OpenAI Whisper API (or compatible endpoint).
/// </summary>
public sealed class AudioTranscribeTool : ITool
{
    public string Name => "AudioTranscribe";
    public string Description => "Transcribe audio (wav/mp3/m4a) to text using OpenAI Whisper API.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            path = new { type = "string", description = "Local path or HTTP/HTTPS URL to the audio file" },
            model = new { type = "string", description = "OpenAI model (default whisper-1)" },
            language = new { type = "string", description = "Optional language hint (e.g., 'en')" }
        },
        required = new[] { "path" }
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AudioTranscribeTool> _logger;

    public AudioTranscribeTool(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<AudioTranscribeTool> logger)
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

        var model = args.TryGetProperty("model", out var modelProp) && modelProp.ValueKind == JsonValueKind.String
            ? (modelProp.GetString() ?? "whisper-1")
            : "whisper-1";
        var language = args.TryGetProperty("language", out var langProp) && langProp.ValueKind == JsonValueKind.String
            ? langProp.GetString()
            : null;

        try
        {
            var apiKey = _configuration["OpenAI:ApiKey"] ?? _configuration["OpenAI:SpeechKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new { success = false, error = "OpenAI API key not configured (set OpenAI:ApiKey)" };
            }

            var endpoint = _configuration["OpenAI:AudioEndpoint"] ?? "https://api.openai.com/v1/audio/transcriptions";
            var localPath = await EnsureLocalFileAsync(input, ct);
            if (localPath is null || !File.Exists(localPath))
            {
                return new { success = false, error = "Audio file not found" };
            }

            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            using var content = new MultipartFormDataContent();
            await using var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var streamContent = new StreamContent(fs);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            content.Add(streamContent, "file", Path.GetFileName(localPath));
            content.Add(new StringContent(model), "model");
            if (!string.IsNullOrWhiteSpace(language))
            {
                content.Add(new StringContent(language), "language");
            }

            request.Content = content;
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"Audio transcription failed: {response.StatusCode} - {error}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var text = doc.RootElement.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? string.Empty : doc.RootElement.ToString();

            return new
            {
                success = true,
                model,
                language,
                characters = text.Length,
                preview = text.Length > 400 ? text.Substring(0, 400) + "..." : text,
                text
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audio transcription failed for {Path}", input);
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
            if (string.IsNullOrWhiteSpace(extension)) extension = ".mp3";
            var tempFile = Path.Combine(Path.GetTempPath(), $"audio_{Guid.NewGuid():N}{extension}");
            await using var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs, ct);
            return tempFile;
        }

        return File.Exists(input) ? input : null;
    }
}
