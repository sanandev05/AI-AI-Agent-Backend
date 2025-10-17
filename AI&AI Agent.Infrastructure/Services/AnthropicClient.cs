using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace AI_AI_Agent.Infrastructure.Services
{
    public class AnthropicClient
    {
        private readonly HttpClient _http;
        private readonly string? _apiKey;

        public AnthropicClient(HttpClient http, IConfiguration config)
        {
            _http = http;
            _apiKey = config["Anthropic:ApiKey"];
            _http.BaseAddress = new Uri("https://api.anthropic.com/");
        }

        public async Task<string> CompleteAsync(string model, string prompt, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("Anthropic API key is not configured (Anthropic:ApiKey).");

            var req = new HttpRequestMessage(HttpMethod.Post, "v1/messages");
            req.Headers.Add("x-api-key", _apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");

            var body = new
            {
                model,
                max_tokens = 1024,
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt ?? string.Empty }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(body);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("content", out var contentArr) || contentArr.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }
            var sb = new StringBuilder();
            foreach (var block in contentArr.EnumerateArray())
            {
                if (block.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                {
                    sb.Append(t.GetString());
                }
            }
            return sb.ToString();
        }
    }
}
