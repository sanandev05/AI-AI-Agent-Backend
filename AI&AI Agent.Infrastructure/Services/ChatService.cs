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

namespace AI_AI_Agent.Application.Services
{
    public class ChatService : IChatService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IGenericService<MessageDto, Message> _messageService;
        private readonly IMapper _mapper;
        private readonly IChatRepository _chatRepo;
        private readonly Kernel _kernel;
        public ChatService(IServiceProvider serviceProvider, Kernel kernel, IGenericService<MessageDto, Message> messageService, IChatRepository chatRepo, IMapper mapper)
        {
            _serviceProvider = serviceProvider;
            _kernel = kernel;
            _messageService = messageService;
            _chatRepo = chatRepo;
            _mapper = mapper;
        }





        public async Task<ChatDto> CreateChatAsync(string userId)
        {
            Chat chat = new Chat()
            {
                Messages = new(),
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

            // 1. Get Chat
            var chat = await _chatRepo.GetByIdAsync(Guid.Parse(request.ChatId), c => c.Messages);
            var userMessage = new Message
            {
                Content = request.Message,
                Roles = MessageRole.User,
                ChatId = chat.ChatGuid,
            };

            // Add the new message to the in-memory list for history creation
            var messagesForHistory = chat.Messages.OrderBy(m => m.CreatedAt).ToList();
            messagesForHistory.Add(userMessage);

            // 2. Construct chat history for AI from the complete list
            var history = new ChatHistory();
            foreach (var message in messagesForHistory)
            {
                var authorRole = message.Roles == MessageRole.User ? AuthorRole.User : AuthorRole.Assistant;
                history.AddMessage(authorRole, message.Content);
            }

            // 3. Save the new user message to the database
            await _messageService.AddAsync(_mapper.Map<MessageDto>(userMessage));

            // 4. Stream AI response
            var fullResponse = new System.Text.StringBuilder();
            var response = chatCompletionService.GetStreamingChatMessageContentsAsync(history, kernel: _kernel);

            await foreach (var chunk in response)
            {
                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    fullResponse.Append(chunk.Content);
                    yield return chunk.Content;
                }
            }

            // 5. Save assistant message
            var assistantMessage = new Message
            {
                Content = fullResponse.ToString(),
                Roles = MessageRole.Assistant,
                ChatId = chat.ChatGuid,
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
    }
}
