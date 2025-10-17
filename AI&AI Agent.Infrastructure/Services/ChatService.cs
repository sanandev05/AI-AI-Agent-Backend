using AI_AI_Agent.Contract.Services;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using System.Reflection;
using AI_AI_Agent.Contract.DTOs;
using AI_AI_Agent.Domain.Entities;
using AI_AI_Agent.Domain.Entities.Enums;
using AI_AI_Agent.Infrastructure.ChatClasses;
using AI_AI_Agent.Domain.Repositories;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using AI_AI_Agent.Domain.Helpers;
// using AI_AI_Agent.Infrastructure.Services; // no direct Anthropic usage

namespace AI_AI_Agent.Application.Services
{
    public class ChatService : IChatService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IGenericService<MessageDto, Message> _messageService;
        private readonly IMapper _mapper;
        private readonly IChatRepository _chatRepo;
        private readonly Kernel _kernel;
        private readonly ILogger<ChatService> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IGoogleSearchService _googleSearchService;

        public ChatService(
            IServiceProvider serviceProvider,
            Kernel kernel,
            IGenericService<MessageDto, Message> messageService,
            IChatRepository chatRepo,
            IMapper mapper,
            ILogger<ChatService> logger,
            IWebHostEnvironment webHostEnvironment,
            IHttpContextAccessor httpContextAccessor,
            IGoogleSearchService googleSearchService)
        {
            _serviceProvider = serviceProvider;
            _kernel = kernel;
            _messageService = messageService;
            _chatRepo = chatRepo;
            _mapper = mapper;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
            _httpContextAccessor = httpContextAccessor;
            _googleSearchService = googleSearchService;
        }





        public async Task<ChatDto> CreateChatAsync(string userId)
        {
            Chat chat = new Chat()
            {
                Messages = new List<Message>(),
                ChatGuid = Guid.NewGuid(),
                Status = ChatStatus.Active,
                Title = "New Chat",
                TotalTokensConsumed = 0,
                UserId = userId
            };
            await _chatRepo.AddAsync(chat);
            return _mapper.Map<ChatDto>(chat);
        }

        public async Task<IEnumerable<ChatDto>> GetChatsByUserIdAsync(string UserId)
        {
            var getChats = await _chatRepo.GetChatsByUserIdAsync(UserId,c=>c.Messages);
        
            return _mapper.Map<IEnumerable<ChatDto>>(getChats);
        }

        public async IAsyncEnumerable<string> StreamChatAsync(ChatRequestDto request, string userId)
        {
            // Resolve chat completion service:
            // 1) If a specific ModelKey is provided, use keyed service by that exact id
            // 2) Else if Model string is provided, try it as a keyed id first; if not found, treat it as provider name
            // 3) Else if Provider string is provided, try as keyed service
            // 4) Else fallback to provider by numeric alias ("1"=OpenAI, "2"=Google)
            // 5) Else fallback to default registered service
            IChatCompletionService? chatCompletionService = null;
            string? selectedServiceId = null;
            if (!string.IsNullOrWhiteSpace(request.ModelKey))
            {
                try { chatCompletionService = _kernel.Services.GetRequiredKeyedService<IChatCompletionService>(request.ModelKey!); selectedServiceId = request.ModelKey; } catch { }
            }
            if (chatCompletionService is null && !string.IsNullOrWhiteSpace(request.Model))
            {
                var modelPref = request.Model.Trim();
                // Try as an exact keyed model id (e.g., gpt-4o, gemini-2.5-flash)
                try { chatCompletionService = _kernel.Services.GetRequiredKeyedService<IChatCompletionService>(modelPref); selectedServiceId = modelPref; }
                catch { /* ignore */ }
                // If not found, try as provider name
                if (chatCompletionService is null)
                {
                    try { chatCompletionService = _kernel.Services.GetRequiredKeyedService<IChatCompletionService>(NormalizeProvider(modelPref)); selectedServiceId = NormalizeProvider(modelPref); } catch { }
                }
            }
            if (chatCompletionService is null && !string.IsNullOrWhiteSpace(request.Provider))
            {
                try { chatCompletionService = _kernel.Services.GetRequiredKeyedService<IChatCompletionService>(request.Provider!.Trim()); selectedServiceId = request.Provider!.Trim(); } catch { }
            }
            if (chatCompletionService is null)
            {
                try
                {
                    // Back-compat: numeric enum alias where 1=OpenAI, 2=Google
                    var alias = (request.Model ?? string.Empty).Trim();
                    var key = alias switch { "1" => "OpenAI", "2" => "Google", _ => string.Empty };
                    if (!string.IsNullOrEmpty(key))
                    {
                        chatCompletionService = _kernel.Services.GetRequiredKeyedService<IChatCompletionService>(key);
                        selectedServiceId = key;
                    }
                }
                catch { /* will fallback below */ }
            }
            if (chatCompletionService is null)
            {
                chatCompletionService = _kernel.Services.GetService<IChatCompletionService>();
            }
            if (chatCompletionService is null)
            {
                _logger.LogError("No chat completion service is configured. Using echo fallback for development.");
                // Dev-only fallback: stream back the user's message with a prefix
                var echo = $"[dev-fallback] {request.Message}";
                foreach (var ch in echo.Chunk(32))
                {
                    yield return new string(ch);
                    await Task.Delay(10);
                }
                yield return JsonSerializer.Serialize(new { type = "usage", data = new { promptTokens = 0, completionTokens = 0, totalTokens = 0 } });
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(selectedServiceId))
            {
                _logger.LogInformation("Using chat completion serviceId: {ServiceId}", selectedServiceId);
            }

            // 1. Get Chat and create the in-memory message for the current user turn
            if (!Guid.TryParse(request.ChatId, out var chatGuid))
            {
                _logger.LogWarning("StreamChatAsync: invalid ChatId format {ChatId}", request.ChatId);
                yield return JsonSerializer.Serialize(new { type = "error", data = "Invalid ChatId." });
                yield break;
            }
            var chat = await _chatRepo.GetByIdAsync(chatGuid, c => c.Messages);
            if (chat == null || chat.UserId != userId)
            {
                yield return JsonSerializer.Serialize(new { type = "error", data = "Chat not found or access denied." });
                yield break;
            }
            var userMessage = new Message
            {
                Content = request.Message,
                Roles = MessageRole.User,
                ChatId = chat.ChatGuid,
                ImageUrls = request.ImageUrls is not null && request.ImageUrls.Any()
                    ? string.Join(";", request.ImageUrls)
                    : null,
                TimeStamp = DateTime.UtcNow // Ensure timestamp is set for ordering
            };

            _logger.LogInformation("Received chat request for ChatId {ChatId}. Image URLs provided: {ImageUrls}", request.ChatId, userMessage.ImageUrls ?? "None");

            // Auto-generate title for new chats
            if (chat.Title == "New Chat")
            {
                var titlePrompt = $"Generate a very short, concise title (less than 5 words) for the following user query. Do not add any prefixes like 'Title:'.\n\nUser Query: {userMessage.Content}";
                var titleResult = await chatCompletionService.GetChatMessageContentAsync(titlePrompt, kernel: _kernel);
                if (!string.IsNullOrWhiteSpace(titleResult.Content))
                {
                    chat.Title = titleResult.Content.Trim('"', '.');
                    await _chatRepo.UpdateAsync(chat);
                }
            }

            // 2. Construct chat history for the AI
            var history = new ChatHistory();
            const int MaxMessagesToKeep = 10; 

            // Ensure chat.Messages is not null before querying it
            var messages = chat.Messages ?? new List<Message>();

            // Get the most recent messages from the database
            var recentMessages = messages
                .OrderByDescending(m => m.TimeStamp)
                .Take(MaxMessagesToKeep - 1) // Take one less to make space for the new message
                .OrderBy(m => m.TimeStamp)
                .ToList();

            // Add historical messages to the history object
            foreach (var message in recentMessages)
            {
                var authorRole = message.Roles == MessageRole.User ? AuthorRole.User : AuthorRole.Assistant;
                var chatMessage = new ChatMessageContent(authorRole, message.Content);

                if (!string.IsNullOrEmpty(message.ImageUrls))
                {
                    var urls = message.ImageUrls.Split(';');
                    foreach (var url in urls)
                    {
                        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                        {
                            // Check if the URL is local
                            var httpRequest = _httpContextAccessor.HttpContext?.Request;
                            bool isLocal = httpRequest != null && (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals(httpRequest.Host.Host, StringComparison.OrdinalIgnoreCase));

                            if (isLocal)
                            {
                                _logger.LogInformation("Processing local historical image URL: {ImageUrl}", url);
                                var fileName = Path.GetFileName(uri.AbsolutePath);
                                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "images", fileName);

                                if (File.Exists(filePath))
                                {
                                    try
                                    {
                                        // We have to read synchronously here because we are in a regular foreach loop.
                                        // Consider refactoring to async if performance becomes an issue.
                                        var bytes = File.ReadAllBytes(filePath);
                                        var mimeType = GetMimeType(fileName);
                                        chatMessage.Items.Add(new ImageContent(bytes, mimeType));
                                        _logger.LogInformation("Successfully added historical local image as byte data. MimeType: {MimeType}", mimeType);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error reading historical local image file: {FilePath}", filePath);
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("Historical local image file not found: {FilePath}", filePath);
                                }
                            }
                            else
                            {
                                // It's a public URL, add it directly
                                _logger.LogInformation("Adding public historical image URL to message: {ImageUrl}", uri);
                                chatMessage.Items.Add(new ImageContent(uri));
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Could not create a valid URI from historical URL: {ImageUrl}", url);
                        }
                    }
                }
                history.Add(chatMessage);
            }

            // Now, create and add the current user's message to the history
            var userChatMessage = new ChatMessageContent(AuthorRole.User, userMessage.Content);
            if (!string.IsNullOrEmpty(userMessage.ImageUrls))
            {
                var urls = userMessage.ImageUrls.Split(';');
                foreach (var url in urls)
                {
                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        // Check if the URL is local
                        var httpRequest = _httpContextAccessor.HttpContext?.Request;
                        bool isLocal = httpRequest != null && (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals(httpRequest.Host.Host, StringComparison.OrdinalIgnoreCase));

                        if (isLocal)
                        {
                            _logger.LogInformation("Processing local image URL: {ImageUrl}", url);
                            var fileName = Path.GetFileName(uri.AbsolutePath);
                            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "images", fileName);

                            if (File.Exists(filePath))
                            {
                                try
                                {
                                    var bytes = await File.ReadAllBytesAsync(filePath);
                                    var mimeType = GetMimeType(fileName);
                                    userChatMessage.Items.Add(new ImageContent(bytes, mimeType));
                                    _logger.LogInformation("Successfully added local image as byte data. MimeType: {MimeType}", mimeType);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error reading local image file: {FilePath}", filePath);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Local image file not found: {FilePath}", filePath);
                            }
                        }
                        else
                        {
                            // It's a public URL, add it directly
                            _logger.LogInformation("Adding public image URL to user message: {ImageUrl}", uri);
                            userChatMessage.Items.Add(new ImageContent(uri));
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Could not create a valid URI from: {ImageUrl}", url);
                    }
                }
            }
            history.Add(userChatMessage);

            _logger.LogInformation("Constructed chat history with {HistoryCount} messages. Sending to AI.", history.Count);

            // 3. Save the new user message to the database
            await _messageService.AddAsync(_mapper.Map<MessageDto>(userMessage));

            // 4. Stream AI response and capture final token metadata
            // Use StreamTextAssembler to eliminate duplicate chunks automatically
            var textAssembler = new StreamTextAssembler();
            var response = chatCompletionService.GetStreamingChatMessageContentsAsync(history, kernel: _kernel);
            int promptTokens = 0;
            int completionTokens = 0;
            int totalTokens = 0;
            StreamingChatMessageContent? finalChunk = null;

            await foreach (var chunk in response)
            {
                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    var delta = textAssembler.AppendAndGetDelta(chunk.Content);
                    if (!string.IsNullOrEmpty(delta))
                    {
                        yield return delta;
                    }
                }

                // The last chunk with metadata should have the final usage stats.
                if (chunk.Metadata is not null)
                {
                    finalChunk = chunk;
                }
            }

            // After the stream is complete, process the final chunk's metadata
            if (finalChunk?.Metadata is not null)
            {
                // Handle OpenAI-specific usage metadata (ChatTokenUsage)
                if (finalChunk.Metadata.TryGetValue("Usage", out var usageObj) && usageObj is not null)
                {
                    try
                    {
                        if (usageObj is OpenAI.Chat.ChatTokenUsage usage)
                        {
                            promptTokens = usage.InputTokenCount;
                            completionTokens = usage.OutputTokenCount;
                            totalTokens = usage.TotalTokenCount;
                        }
                    }
                    catch
                    {
                        // fallback if cast fails
                    }
                }

                // Handle Gemini and other providers
                else
                {
                    if (finalChunk.Metadata.TryGetValue("PromptTokens", out var p) && p is int pToken && pToken > 0)
                        promptTokens = pToken;
                    if (finalChunk.Metadata.TryGetValue("prompt_token_count", out var p2) && p2 is int pToken2 && pToken2 > 0)
                        promptTokens = pToken2;

                    if (finalChunk.Metadata.TryGetValue("CompletionTokens", out var c) && c is int cToken && cToken > 0)
                        completionTokens = cToken;
                    if (finalChunk.Metadata.TryGetValue("candidates_token_count", out var c2) && c2 is int cToken2 && cToken2 > 0)
                        completionTokens = cToken2;

                    if (finalChunk.Metadata.TryGetValue("TotalTokens", out var t) && t is int tToken && tToken > 0)
                        totalTokens = tToken;
                    if (finalChunk.Metadata.TryGetValue("total_token_count", out var t2) && t2 is int tToken2 && tToken2 > 0)
                        totalTokens = tToken2;
                }
            }


            // If total tokens is not provided, calculate it.
            if (totalTokens == 0 && promptTokens > 0 && completionTokens > 0)
            {
                totalTokens = promptTokens + completionTokens;
            }

            // 5. Save assistant message (using deduplicated text from assembler)
            var assistantMessage = new Message
            {
                Content = textAssembler.GetText(),
                Roles = MessageRole.Assistant,
                ChatId = chat.ChatGuid,
                InputToken = promptTokens,
                OutputToken = completionTokens,
                TotalToken = totalTokens
            };
            await _messageService.AddAsync(_mapper.Map<MessageDto>(assistantMessage));

            // 6. Emit usage as a final JSON event so UI can display token/cost
            var usagePayload = new
            {
                type = "usage",
                data = new { promptTokens, completionTokens, totalTokens }
            };
            yield return JsonSerializer.Serialize(usagePayload);
        }

        private static string NormalizeProvider(string s)
        {
            return s.Equals("openai", StringComparison.OrdinalIgnoreCase) ? "OpenAI"
                 : s.Equals("google", StringComparison.OrdinalIgnoreCase) ? "Google"
                 : s;
        }

        


        public async Task<ChatDto> GetChatByUIdAsync(Guid uId, string userId)
        {
            var chat = await _chatRepo.GetByIdAsync(uId, c => c.Messages);
            if (chat == null || chat.UserId != userId)
            {
                return new ChatDto();
            }
            return _mapper.Map<ChatDto>(chat);
        }

        public async Task<bool> DeleteChatAsync(Guid uId, string userId)
        {
            var chat = await _chatRepo.GetByIdAsync(uId);
            if (chat == null || chat.UserId != userId)
            {
                return false;
            }

            await _chatRepo.DeleteAsync(chat.ChatGuid);
            return true;
        }

        public async Task<bool> RenameChatAsync(Guid uId, string newTitle, string userId)
        {
            var chat = await _chatRepo.GetByIdAsync(uId);
            if (chat == null || chat.UserId != userId)
            {
                return false;
            }

            chat.Title = newTitle;
            await _chatRepo.UpdateAsync(chat);
            return true;
        }

        public async IAsyncEnumerable<string> StreamWebSearchAsync(WebSearchRequestDto request, string userId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            // 1. Validate ChatId and user access
            if (!Guid.TryParse(request.ChatId, out var chatGuid))
            {
                yield return JsonSerializer.Serialize(new { type = "error", data = "Invalid ChatId format." });
                yield break;
            }
            var chat = await _chatRepo.GetByIdAsync(chatGuid);
            if (chat == null || chat.UserId != userId)
            {
                yield return JsonSerializer.Serialize(new { type = "error", data = "Chat not found or access denied." });
                yield break;
            }

            // 2. Save the user's message
            var q = request.Query ?? string.Empty;
            var userMessage = new Message
            {
                Content = q,
                Roles = MessageRole.User,
                ChatId = chatGuid,
                TimeStamp = DateTime.UtcNow
            };
            await _messageService.AddAsync(_mapper.Map<MessageDto>(userMessage));

            var streamChunks = new List<string>();
            string fullSummary = "";
            bool hasError = false;
            string errorMessage = "";

            try
            {
                streamChunks.Add(JsonSerializer.Serialize(new { type = "status", data = "Searching the web..." }));
                var searchResult = await _googleSearchService.SearchAsync(q);

                if (!string.IsNullOrEmpty(searchResult.Error) || (searchResult.Results?.Any() != true))
                {
                    hasError = true;
                    // Provide more helpful error message
                    if (!string.IsNullOrEmpty(searchResult.Error))
                    {
                        errorMessage = $"Web search failed: {searchResult.Error}. Please check if your Google Custom Search API is configured correctly in user secrets.";
                    }
                    else
                    {
                        errorMessage = "No results found for your query. Try rephrasing your search or check API configuration.";
                    }
                    _logger.LogWarning("Web search returned no results or error for query '{Query}': {Error}", q, searchResult.Error ?? "No results");
                }
                else
                {
                    streamChunks.Add(JsonSerializer.Serialize(new { type = "status", data = "Analyzing results and synthesizing answer..." }));

                    // Enhanced formatting with more context for the AI
                    var formattedResults = string.Join("\n\n", (searchResult.Results ?? new List<WebSearchResult>()).Select((r, i) => 
                        $"## Source [{i + 1}]\n" +
                        $"**Title:** {r.Title}\n" +
                        $"**Content:** {r.Snippet}\n" +
                        $"**URL:** {r.Url}"));
                    
                    _logger.LogInformation("Summarization input prepared with {Count} sources (total len={Len})", searchResult.Results?.Count ?? 0, formattedResults.Length);

                    // If no chat model is configured, do a very simple local summarization
                    var chatService = _kernel.Services.GetService<IChatCompletionService>();
                    if (chatService is null)
                    {
                        var lines = formattedResults.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).Take(24).ToList();
                        var head = string.Join("\n", lines);
                        var local = $"Summary for '{q}':\n" + head;
                        foreach (var chunk in local.Chunk(64))
                        {
                            var piece = new string(chunk);
                            streamChunks.Add(JsonSerializer.Serialize(new { type = "summary_chunk", data = piece }));
                        }
                        fullSummary = local;
                    }
                    else
                    {
                        var summarizationPrompt = @"
You are an expert research assistant providing direct, accurate answers based on web search results.

🎯 **CRITICAL INSTRUCTIONS:**

1. **DIRECTLY ANSWER THE QUESTION FIRST**
   - Start immediately with the specific answer the user wants
   - Don't start with ""Let me research..."" or ""Here's what I found...""
   - Get straight to the point in the first sentence

2. **BE PRECISE AND SPECIFIC**
   - Include exact numbers, dates, names, prices, locations
   - If the query asks ""when"", give the date/time
   - If the query asks ""who"", give the name(s)
   - If the query asks ""how much"", give the price/amount
   - If the query asks ""where"", give the location

3. **STRUCTURE YOUR RESPONSE**
   - **First paragraph:** Direct answer with key facts
   - **Following paragraphs:** Supporting details and context
   - Use bullet points for lists and multiple items
   - Use **bold** for important facts
   - Add section headers (##) only if multiple topics

4. **CITE SOURCES INLINE**
   - Use [1], [2], [3] right after each fact
   - Add ""**Sources:**"" section at the end with URLs

5. **HANDLE AMBIGUOUS QUERIES**
   - If query is vague, cover the most likely interpretations
   - Example: ""first person"" → could mean ""first human"", ""richest person"", ""most powerful person""
   - Address each interpretation briefly

6. **BE CONVERSATIONAL BUT AUTHORITATIVE**
   - Write naturally, as if explaining to a colleague
   - Avoid overly formal or academic language
   - But remain factual and credible

---

**Examples of GOOD responses:**

Query: ""When is the next iPhone release?""
✅ Good: ""The next iPhone 16 is expected to launch in **September 2025**, following Apple's typical annual release pattern. The keynote event is likely scheduled for mid-September, with devices shipping shortly after. [1]

Key expected features include... [2]

**Sources:**
[1] techcrunch.com/article
[2] macrumors.com/iphone-16""

❌ Bad: ""Based on the search results, I found information about iPhone releases. Let me break this down into sections...""

---

Query: ""Who won the election in Argentina?""
✅ Good: ""**Javier Milei** won Argentina's presidential election in November 2023. He defeated Economy Minister Sergio Massa in the runoff election with 55.7% of the vote. [1] Milei is a libertarian economist who campaigned on radical economic reforms. [2]""

❌ Bad: ""The search results show information about elections. Here's what I found:...""

---

**User Query:** {{$query}}

**Search Results:**
{{$results}}

---

**Your Direct Answer:**
                        ";

                        var summarizerFunction = _kernel.CreateFunctionFromPrompt(summarizationPrompt);
                        
                        // Use optimal settings for web search synthesis - lower temperature for accuracy
                        var executionSettings = new PromptExecutionSettings
                        {
                            ExtensionData = new Dictionary<string, object>
                            {
                                ["temperature"] = 0.3,      // Low temperature for factual accuracy
                                ["max_tokens"] = 2500,      // Allow comprehensive answers
                                ["top_p"] = 0.9            // Focus on high-probability tokens
                            }
                        };
                        
                        var summaryResultStream = _kernel.InvokeStreamingAsync<StreamingChatMessageContent>(
                            summarizerFunction, 
                            new KernelArguments(executionSettings) 
                            { 
                                { "query", q }, 
                                { "results", formattedResults } 
                            });

                        // Use StreamTextAssembler to eliminate duplicate chunks
                        var textAssembler = new StreamTextAssembler();
                        await foreach (var chunk in summaryResultStream)
                        {
                            if (chunk.Content is not null)
                            {
                                var piece = textAssembler.AppendAndGetDelta(chunk.Content);
                                if (!string.IsNullOrEmpty(piece))
                                {
                                    streamChunks.Add(JsonSerializer.Serialize(new { type = "summary_chunk", data = piece }));
                                }
                            }
                        }
                        fullSummary = textAssembler.GetText();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during the web search stream for ChatId {ChatId}", chatGuid);
                hasError = true;
                errorMessage = $"An internal error occurred: {ex.Message}";
            }

            foreach (var chunk in streamChunks)
            {
                yield return chunk;
            }

            if (hasError)
            {
                yield return JsonSerializer.Serialize(new { type = "error", data = errorMessage });
                var errorAssistantMessage = new Message { Content = errorMessage, Roles = MessageRole.Assistant, ChatId = chatGuid, TimeStamp = DateTime.UtcNow };
                await _messageService.AddAsync(_mapper.Map<MessageDto>(errorAssistantMessage));
                yield break;
            }

            if (!string.IsNullOrEmpty(fullSummary))
            {
                var assistantMessage = new Message
                {
                    Content = fullSummary,
                    Roles = MessageRole.Assistant,
                    ChatId = chatGuid,
                    TimeStamp = DateTime.UtcNow
                };
                await _messageService.AddAsync(_mapper.Map<MessageDto>(assistantMessage));
            }
        }



        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream",
            };
        }
    }
}
