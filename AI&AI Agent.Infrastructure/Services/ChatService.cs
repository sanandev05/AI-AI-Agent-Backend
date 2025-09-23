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
            var chatCompletionService = _kernel.Services.GetRequiredKeyedService<IChatCompletionService>(request.Model.ToString());

            // 1. Get Chat and create the in-memory message for the current user turn
            var chat = await _chatRepo.GetByIdAsync(Guid.Parse(request.ChatId), c => c.Messages);
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
            var fullResponse = new System.Text.StringBuilder();
            var response = chatCompletionService.GetStreamingChatMessageContentsAsync(history, kernel: _kernel);
            int promptTokens = 0;
            int completionTokens = 0;
            int totalTokens = 0;
            StreamingChatMessageContent? finalChunk = null;

            await foreach (var chunk in response)
            {
                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    fullResponse.Append(chunk.Content);
                    yield return chunk.Content;
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

            // 5. Save assistant message
            var assistantMessage = new Message
            {
                Content = fullResponse.ToString(),
                Roles = MessageRole.Assistant,
                ChatId = chat.ChatGuid,
                InputToken = promptTokens,
                OutputToken = completionTokens,
                TotalToken = totalTokens
            };
            await _messageService.AddAsync(_mapper.Map<MessageDto>(assistantMessage));
        }


        public async Task<ChatDto> GetChatByUIdAsync(Guid uId, string userId)
        {
            var chat = await _chatRepo.GetByIdAsync(uId, c => c.Messages);
            if (chat == null || chat.UserId != userId)
            {
                return null;
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
            var userMessage = new Message
            {
                Content = request.Query,
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
                var searchResult = await _googleSearchService.SearchAsync(request.Query);

                if (!string.IsNullOrEmpty(searchResult.Error) || (searchResult.Results?.Any() != true))
                {
                    hasError = true;
                    errorMessage = searchResult.Error ?? "No results found or failed to retrieve content.";
                }
                else
                {
                    streamChunks.Add(JsonSerializer.Serialize(new { type = "status", data = "Synthesizing answer..." }));
                    
                    var summarizationPrompt = @"
                        Based on the following search results, provide a comprehensive answer to the user's query.
                        Combine the information from the snippets to form a coherent response.
                        Cite the sources using footnotes like [1], [2], etc., at the end of the relevant sentences.
                        
                        User Query: {{$query}}

                        Search Results:
                        {{$results}}
                        
                        Answer:
                    ";
                    
                    var formattedResults = string.Join("\n\n", searchResult.Results.Select((r, i) => $"[{i + 1}] Title: {r.Title}\nSnippet: {r.Snippet}\nURL: {r.Url}"));
                    _logger.LogInformation("Sending the following formatted results to the AI for summarization:\n{FormattedResults}", formattedResults);

                    var summarizerFunction = _kernel.CreateFunctionFromPrompt(summarizationPrompt);
                    var summaryResultStream = _kernel.InvokeStreamingAsync<StreamingChatMessageContent>(summarizerFunction, new() { { "query", request.Query }, { "results", formattedResults } });

                    var summaryBuilder = new System.Text.StringBuilder();
                    await foreach (var chunk in summaryResultStream)
                    {
                        if (chunk.Content is not null)
                        {
                            summaryBuilder.Append(chunk.Content);
                            streamChunks.Add(JsonSerializer.Serialize(new { type = "summary_chunk", data = chunk.Content }));
                        }
                    }
                    fullSummary = summaryBuilder.ToString();
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
