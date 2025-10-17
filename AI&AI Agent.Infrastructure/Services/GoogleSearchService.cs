using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Contract.DTOs;
using AI_AI_Agent.Contract.Services;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AI_AI_Agent.Infrastructure.Services
{
    public class GoogleSearchService : IGoogleSearchService, IWebSearchService
    {
        private const int DefaultMaxResults = 8;
        private const string WebSearchPluginTypeName = "Microsoft.SemanticKernel.Plugins.Web.WebSearchEnginePlugin, Microsoft.SemanticKernel.Plugins.Web";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GoogleSearchService> _logger;
    private readonly string? _apiKey;
    private readonly string? _searchEngineId;
        private readonly string? _searchProvider;
        private readonly string? _bingApiKey;
        private readonly string? _serpApiKey;
        private readonly string? _tavilyApiKey;

        public GoogleSearchService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GoogleSearchService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;

            // Prefer dedicated Search:Google keys to avoid conflicts with Gemini's Google:ApiKey
            _apiKey = configuration["Search:Google:ApiKey"] ?? configuration["Google:ApiKey"];
            _searchEngineId = configuration["Search:Google:Cx"] ?? configuration["Google:SearchEngineId"];
            _searchProvider = configuration["Search:Provider"];

            _bingApiKey = configuration["Search:Bing:ApiKey"];
            if (string.IsNullOrWhiteSpace(_bingApiKey))
            {
                _bingApiKey = configuration["Bing:ApiKey"];
            }

            _serpApiKey = configuration["Search:SerpApi:ApiKey"];
            _tavilyApiKey = configuration["Search:Tavily:ApiKey"];

            _logger.LogInformation(
                "Search service configured: provider={Provider}, googleKey={HasGoogleKey}, searchEngineId={HasCx}, bingKey={HasBing}, serpKey={HasSerp}",
                _searchProvider ?? "(default)",
                HasGoogleKeys(),
                !string.IsNullOrWhiteSpace(_searchEngineId) && _searchEngineId != "SET_IN_USER_SECRETS",
                HasBingKey(),
                HasSerpKey());
        }

        public Task<WebSearchResultDto> SearchAsync(string query, CancellationToken ct = default)
            => SearchInternalAsync(query, ct);

        public async IAsyncEnumerable<string> StreamSearchAsync(string query, [EnumeratorCancellation] CancellationToken ct = default)
        {
            var result = await SearchInternalAsync(query, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                yield return $"error: {result.Error}";
                yield break;
            }

            var items = result.Results ?? new List<WebSearchResult>();
            if (items.Count == 0)
            {
                yield return "No results found.";
                yield break;
            }

            for (var i = 0; i < items.Count; i++)
            {
                if (ct.IsCancellationRequested)
                {
                    yield break;
                }

                var entry = items[i];
                var snippet = string.IsNullOrWhiteSpace(entry.Snippet) ? entry.Url : entry.Snippet;
                yield return $"[{i + 1}] {entry.Title} — {snippet}";
            }
        }

        private async Task<WebSearchResultDto> SearchInternalAsync(string query, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new WebSearchResultDto
                {
                    Query = query,
                    Results = new List<WebSearchResult>(),
                    Error = "Query cannot be empty."
                };
            }

            var trimmed = query.Trim();
            _logger.LogInformation("🔎 Attempting Semantic Kernel web search for query: {Query}", trimmed);

            var semanticResults = await TrySemanticKernelSearchAsync(trimmed, DefaultMaxResults, ct).ConfigureAwait(false);
            if (semanticResults.Count > 0)
            {
                return new WebSearchResultDto
                {
                    Query = trimmed,
                    Results = semanticResults
                };
            }

            _logger.LogInformation("Semantic Kernel plugin returned no results for '{Query}'. Falling back to legacy search.", trimmed);
            return await SearchUsingLegacyAsync(trimmed, ct).ConfigureAwait(false);
        }

        private async Task<List<WebSearchResult>> TrySemanticKernelSearchAsync(string query, int maxResults, CancellationToken ct)
        {
            try
            {
                var pluginType = Type.GetType(WebSearchPluginTypeName, throwOnError: false, ignoreCase: false);
                if (pluginType is null)
                {
                    _logger.LogDebug("WebSearchEnginePlugin type not found. Semantic Kernel plugin search skipped.");
                    return new List<WebSearchResult>();
                }

                var connector = CreateSemanticKernelConnector(pluginType.Assembly);
                if (connector is null)
                {
                    _logger.LogDebug("No Semantic Kernel connector available for provider {Provider}.", _searchProvider ?? "(default)");
                    return new List<WebSearchResult>();
                }

                var pluginInstance = Activator.CreateInstance(pluginType, connector);
                if (pluginInstance is null)
                {
                    _logger.LogDebug("Failed to instantiate WebSearchEnginePlugin.");
                    return new List<WebSearchResult>();
                }

                var searchMethod = pluginType.GetMethod("SearchAsync", BindingFlags.Public | BindingFlags.Instance);
                if (searchMethod is null)
                {
                    _logger.LogDebug("SearchAsync method not found on WebSearchEnginePlugin.");
                    return new List<WebSearchResult>();
                }

                var args = BuildSearchAsyncArguments(searchMethod, query, maxResults, ct);
                if (searchMethod.Invoke(pluginInstance, args) is not Task task)
                {
                    _logger.LogDebug("SearchAsync invocation did not return a Task.");
                    return new List<WebSearchResult>();
                }

                await task.ConfigureAwait(false);

                var resultProperty = task.GetType().GetProperty("Result");
                if (resultProperty?.GetValue(task) is not IEnumerable enumerable)
                {
                    _logger.LogDebug("Semantic Kernel search produced no enumerable results.");
                    return new List<WebSearchResult>();
                }

                var results = new List<WebSearchResult>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in enumerable)
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    var mapped = MapPluginResult(item);
                    if (mapped is null || string.IsNullOrWhiteSpace(mapped.Url))
                    {
                        continue;
                    }

                    if (seen.Add(mapped.Url))
                    {
                        results.Add(mapped);
                        if (results.Count >= maxResults)
                        {
                            break;
                        }
                    }
                }

                _logger.LogInformation("Semantic Kernel web search produced {Count} results for '{Query}'.", results.Count, query);
                return results;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Semantic Kernel web search failed for query '{Query}'.", query);
                return new List<WebSearchResult>();
            }
        }

        private object?[] BuildSearchAsyncArguments(MethodInfo method, string query, int maxResults, CancellationToken ct)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 0)
            {
                return Array.Empty<object?>();
            }

            var args = new object?[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];

                if (string.Equals(parameter.Name, "query", StringComparison.OrdinalIgnoreCase))
                {
                    args[i] = query;
                }
                else if (parameter.ParameterType == typeof(CancellationToken))
                {
                    args[i] = ct;
                }
                else if (string.Equals(parameter.Name, "count", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(parameter.Name, "top", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(parameter.Name, "limit", StringComparison.OrdinalIgnoreCase))
                {
                    args[i] = maxResults;
                }
                else if (string.Equals(parameter.Name, "offset", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(parameter.Name, "skip", StringComparison.OrdinalIgnoreCase))
                {
                    args[i] = 0;
                }
                else if (parameter.HasDefaultValue)
                {
                    args[i] = parameter.DefaultValue;
                }
                else if (parameter.ParameterType.IsValueType)
                {
                    args[i] = Activator.CreateInstance(parameter.ParameterType);
                }
                else
                {
                    args[i] = null;
                }
            }

            return args;
        }

        private object? CreateSemanticKernelConnector(Assembly pluginAssembly)
        {
            var provider = (_searchProvider ?? string.Empty).Trim().ToLowerInvariant();

            object? connector = provider switch
            {
                "google" or "google-cse" or "googlecse" or "googleprogrammablesearch" or "googlecustomsearch" when HasGoogleKeys()
                    => TryCreateConnector(pluginAssembly, "Microsoft.SemanticKernel.Plugins.Web.Google.GoogleConnector", _apiKey, _searchEngineId),
                "bing" or "azure-bing" or "azurebing" when HasBingKey()
                    => TryCreateConnector(pluginAssembly, "Microsoft.SemanticKernel.Plugins.Web.Bing.BingConnector", _bingApiKey),
                "serpapi" when HasSerpKey()
                    => TryCreateConnector(pluginAssembly, "Microsoft.SemanticKernel.Plugins.Web.SerpApi.SerpApiConnector", _serpApiKey),
                "tavily" when HasTavilyKey()
                    => TryCreateConnector(pluginAssembly, "Microsoft.SemanticKernel.Plugins.Web.Tavily.TavilyConnector", _tavilyApiKey),
                _ => null
            };

            if (connector is not null)
            {
                return connector;
            }

            if (HasGoogleKeys())
            {
                connector = TryCreateConnector(pluginAssembly, "Microsoft.SemanticKernel.Plugins.Web.Google.GoogleConnector", _apiKey, _searchEngineId);
                if (connector is not null)
                {
                    return connector;
                }
            }

            if (HasSerpKey())
            {
                connector = TryCreateConnector(pluginAssembly, "Microsoft.SemanticKernel.Plugins.Web.SerpApi.SerpApiConnector", _serpApiKey);
                if (connector is not null)
                {
                    return connector;
                }
            }

            if (HasBingKey())
            {
                connector = TryCreateConnector(pluginAssembly, "Microsoft.SemanticKernel.Plugins.Web.Bing.BingConnector", _bingApiKey);
                if (connector is not null)
                {
                    return connector;
                }
            }

            if (HasTavilyKey())
            {
                connector = TryCreateConnector(pluginAssembly, "Microsoft.SemanticKernel.Plugins.Web.Tavily.TavilyConnector", _tavilyApiKey);
                if (connector is not null)
                {
                    return connector;
                }
            }

            return null;
        }

        private object? TryCreateConnector(Assembly assembly, string typeName, params object?[] args)
        {
            try
            {
                var type = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
                if (type is null)
                {
                    _logger.LogDebug("Semantic Kernel connector type {TypeName} not found.", typeName);
                    return null;
                }

                var ctorArgs = TrimTrailingNulls(args);
                return Activator.CreateInstance(type, ctorArgs);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to create Semantic Kernel connector {TypeName}.", typeName);
                return null;
            }
        }

        private static object?[] TrimTrailingNulls(object?[] args)
        {
            if (args.Length == 0)
            {
                return args;
            }

            var list = new List<object?>(args);
            while (list.Count > 0 && list[^1] is null)
            {
                list.RemoveAt(list.Count - 1);
            }

            return list.ToArray();
        }

        private bool HasGoogleKeys()
            => !string.IsNullOrWhiteSpace(_apiKey) && _apiKey != "SET_IN_USER_SECRETS"
               && !string.IsNullOrWhiteSpace(_searchEngineId) && _searchEngineId != "SET_IN_USER_SECRETS";

        private bool HasBingKey()
            => !string.IsNullOrWhiteSpace(_bingApiKey) && _bingApiKey != "SET_IN_USER_SECRETS";

        private bool HasSerpKey()
            => !string.IsNullOrWhiteSpace(_serpApiKey) && _serpApiKey != "SET_IN_USER_SECRETS";

        private bool HasTavilyKey()
            => !string.IsNullOrWhiteSpace(_tavilyApiKey) && _tavilyApiKey != "SET_IN_USER_SECRETS";

        private async Task<WebSearchResultDto> SearchUsingLegacyAsync(string query, CancellationToken ct)
        {
            if (HasGoogleKeys())
            {
                var googleResult = await GoogleCustomSearchAsync(query, ct).ConfigureAwait(false);
                if (googleResult.Results?.Count > 0)
                {
                    return googleResult;
                }

                if (!string.IsNullOrWhiteSpace(googleResult.Error))
                {
                    _logger.LogInformation("Google Custom Search reported '{Error}'. Falling back to Bing for query '{Query}'.", googleResult.Error, query);
                    var fallback = await BingFallbackAsync(query, ct).ConfigureAwait(false);
                    if (fallback.Results?.Count == 0 && string.IsNullOrWhiteSpace(fallback.Error))
                    {
                        fallback.Error = googleResult.Error;
                    }

                    return fallback;
                }
            }

            return await BingFallbackAsync(query, ct).ConfigureAwait(false);
        }

        private async Task<WebSearchResultDto> GoogleCustomSearchAsync(string query, CancellationToken ct)
        {
            var keyPrefix = string.IsNullOrEmpty(_apiKey) ? "NULL" : _apiKey.Substring(0, Math.Min(10, _apiKey.Length));
            _logger.LogInformation("🔑 Using Google API - Key: {KeyPrefix}..., Engine ID: {EngineId}", 
                keyPrefix, 
                _searchEngineId ?? "NULL");
                
            var requestUri = $"https://www.googleapis.com/customsearch/v1?q={Uri.EscapeDataString(query)}&key={_apiKey}&cx={_searchEngineId}&num={Math.Clamp(DefaultMaxResults, 1, 10)}";

            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                using var response = await httpClient.GetAsync(requestUri, ct).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    var errorMessage = $"Google Custom Search returned HTTP {(int)response.StatusCode}";
                    _logger.LogError("❌ Google Custom Search FAILED - Status: {StatusCode}, Query: '{Query}', Response: {Response}", response.StatusCode, query, errorContent);
                    
                    // Extract more specific error from response
                    try
                    {
                        var errorJson = JsonSerializer.Deserialize<JsonElement>(errorContent);
                        if (errorJson.TryGetProperty("error", out var errorObj))
                        {
                            if (errorObj.TryGetProperty("message", out var message))
                            {
                                errorMessage = $"Google API Error: {message.GetString()}";
                            }
                        }
                    }
                    catch { /* Ignore JSON parse errors */ }
                    
                    return new WebSearchResultDto
                    {
                        Query = query,
                        Results = new List<WebSearchResult>(),
                        Error = errorMessage
                    };
                }

                var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogInformation("📥 Google API Response (first 500 chars): {Response}", content.Substring(0, Math.Min(500, content.Length)));
                
                // Use case-insensitive deserialization
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var parsed = JsonSerializer.Deserialize<GoogleSearchResponse>(content ?? "{}", options);
                var items = parsed?.Items ?? new List<GoogleSearchItem>();
                
                _logger.LogInformation("✅ Deserialized {Count} items from Google response", items.Count);

                if (items.Count == 0)
                {
                    _logger.LogWarning("⚠️ Google Custom Search returned 0 items for query '{Query}'. This usually means your Search Engine is configured to search specific sites only, not the entire web. Check your Programmable Search Engine settings at https://programmablesearchengine.google.com/", query);
                    return new WebSearchResultDto
                    {
                        Query = query,
                        Results = new List<WebSearchResult>()
                    };
                }

                var mapped = items
                    .Where(item => !string.IsNullOrWhiteSpace(item?.Link))
                    .Select(item => new WebSearchResult
                    {
                        Title = string.IsNullOrWhiteSpace(item?.Title) ? item?.Link : item!.Title,
                        Url = item?.Link,
                        Snippet = item?.Snippet
                    })
                    .Where(r => !string.IsNullOrWhiteSpace(r.Url))
                    .Take(DefaultMaxResults)
                    .ToList();

                return new WebSearchResultDto
                {
                    Query = query,
                    Results = mapped
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP error during Google Custom Search for query '{Query}'.", query);
                return new WebSearchResultDto
                {
                    Query = query,
                    Results = new List<WebSearchResult>(),
                    Error = ex.Message
                };
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse Google Custom Search response for query '{Query}'.", query);
                return new WebSearchResultDto
                {
                    Query = query,
                    Results = new List<WebSearchResult>(),
                    Error = "Failed to parse Google search results."
                };
            }
        }

        private async Task<WebSearchResultDto> BingFallbackAsync(string query, CancellationToken ct)
        {
            _logger.LogInformation("🔄 Falling back to Bing scraping for: {Query}", query);
            try
            {
                var url = $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}";
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0 Safari/537.36");
                httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
                httpClient.Timeout = TimeSpan.FromSeconds(15);

                var html = await httpClient.GetStringAsync(url, ct).ConfigureAwait(false);
                var parser = new HtmlParser();
                var doc = parser.ParseDocument(html ?? string.Empty);

                var results = new List<WebSearchResult>();
                
                // Try multiple selectors for Bing results
                var algoElements = doc.QuerySelectorAll("li.b_algo");
                if (algoElements.Length == 0)
                {
                    _logger.LogWarning("⚠️ No li.b_algo elements found, trying alternative selectors");
                    algoElements = doc.QuerySelectorAll(".b_algo, #b_results .b_algo, .b_algoSlug");
                }
                
                _logger.LogInformation("📊 Found {Count} Bing result elements", algoElements.Length);
                
                foreach (var element in algoElements)
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    // Try multiple anchor selectors
                    var anchor = element.QuerySelector("h2 a") 
                              ?? element.QuerySelector("a[href]")
                              ?? element.QuerySelector("a");
                    
                    if (anchor is null)
                    {
                        _logger.LogDebug("No anchor found in element");
                        continue;
                    }

                    var href = anchor.GetAttribute("href");
                    if (string.IsNullOrWhiteSpace(href))
                    {
                        _logger.LogDebug("Empty href");
                        continue;
                    }

                    if (!Uri.TryCreate(href, UriKind.Absolute, out var uri))
                    {
                        _logger.LogDebug("Invalid URI: {Href}", href);
                        continue;
                    }

                    var title = anchor.TextContent?.Trim();
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        // Try getting title from h2
                        var h2 = element.QuerySelector("h2");
                        title = h2?.TextContent?.Trim() ?? uri.Host;
                    }

                    // Try multiple snippet selectors
                    var snippetNode = element.QuerySelector("p") 
                                   ?? element.QuerySelector(".b_caption p")
                                   ?? element.QuerySelector(".b_snippet")
                                   ?? element.QuerySelector("div.b_snippet");
                    var snippet = snippetNode?.TextContent?.Trim();

                    results.Add(new WebSearchResult
                    {
                        Title = title,
                        Url = uri.ToString(),
                        Snippet = snippet ?? ""
                    });
                    
                    _logger.LogInformation("✅ Extracted result: {Title}", title);

                    if (results.Count >= DefaultMaxResults)
                    {
                        break;
                    }
                }

                if (results.Count == 0)
                {
                    _logger.LogError("❌ Bing scraping found 0 results. HTML selectors may have changed. Consider using proper Google Search Engine configuration.");
                    return new WebSearchResultDto
                    {
                        Query = query,
                        Results = new List<WebSearchResult>(),
                        Error = "Web search temporarily unavailable. Please configure a proper Google Search Engine that searches the entire web. See GOOGLE_SEARCH_FIX.md for instructions."
                    };
                }

                _logger.LogInformation("🎉 Bing scraping successful: {Count} results extracted", results.Count);
                return new WebSearchResultDto
                {
                    Query = query,
                    Results = results
                };
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Bing fallback failed for query '{Query}'.", query);
                return new WebSearchResultDto
                {
                    Query = query,
                    Results = new List<WebSearchResult>(),
                    Error = "Web search temporarily unavailable. Please try again later."
                };
            }
        }

        private static WebSearchResult? MapPluginResult(object item)
        {
            if (item is null)
            {
                return null;
            }

            var url = TryReadString(item, "Link", "Url", "Source");
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            return new WebSearchResult
            {
                Title = TryReadString(item, "Title", "Name", "Heading") ?? url,
                Url = url,
                Snippet = TryReadString(item, "Snippet", "Summary", "Description", "Text", "Content")
            };
        }

        private static string? TryReadString(object item, params string[] propertyNames)
        {
            var type = item.GetType();
            foreach (var propertyName in propertyNames)
            {
                var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property is null)
                {
                    continue;
                }

                var value = property.GetValue(item);
                if (value is null)
                {
                    continue;
                }

                switch (value)
                {
                    case string s when !string.IsNullOrWhiteSpace(s):
                        return s.Trim();
                    case Uri uri:
                        return uri.ToString();
                    case IEnumerable enumerable:
                    {
                        var tokens = new List<string>();
                        foreach (var element in enumerable)
                        {
                            if (element is string str && !string.IsNullOrWhiteSpace(str))
                            {
                                tokens.Add(str.Trim());
                            }
                        }

                        if (tokens.Count > 0)
                        {
                            return string.Join(" ", tokens);
                        }

                        break;
                    }
                }
            }

            return null;
        }

        public class GoogleSearchResponse
        {
            public List<GoogleSearchItem>? Items { get; set; }
        }

        public class GoogleSearchItem
        {
            public string? Title { get; set; }
            public string? Link { get; set; }
            public string? Snippet { get; set; }
        }
    }
}
