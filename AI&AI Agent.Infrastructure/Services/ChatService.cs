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

namespace AI_AI_Agent.Application.Services
{
    public class ChatService : IChatService
    {
        private readonly IChatCompletionService _chatCompletionService;
        private readonly IGenericService<MessageDto, Message> _messageService;
        private readonly IMapper _mapper;
        private readonly IChatRepository _chatRepo;
        private readonly Kernel _kernel;
        public ChatService(IChatCompletionService chatCompletionService, Kernel kernel, IGenericService<MessageDto, Message> messageService, IChatRepository chatRepo, IMapper mapper)
        {
            _chatCompletionService = chatCompletionService;
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

        public async Task<ChatDto> GetChatsByUserIdAsync(string UserId)
        {
            var getChats = await _chatRepo.GetChatsByUserIdAsync(UserId,c=>c.Messages);
        
            return _mapper.Map<ChatDto>(getChats);
        }

        private async Task SendMessage(string content, MessageRole role)
        {
            ChatHistory history = [];
            history.AddUserMessage(content);

            var response = await _chatCompletionService.GetChatMessageContentAsync(
                history,
                kernel: _kernel
            );

            int tokensUsed = 0;
            int inputToken = 0;
            int outputToken = 0;
            if (response.Metadata.TryGetValue("Usage", out var usageObj)
                && usageObj is OpenAI.Chat.ChatTokenUsage usage)
            {
                tokensUsed = usage.TotalTokenCount;
                Console.WriteLine($"Prompt: {usage.InputTokenCount}, " +
                                  $"Completion: {usage.OutputTokenCount}, " +
                                  $"Total: {usage.TotalTokenCount}");

                tokensUsed = usage.TotalTokenCount;
                inputToken = usage.InputTokenCount;
                outputToken = usage.OutputTokenCount;
            }

        }
    }
}
