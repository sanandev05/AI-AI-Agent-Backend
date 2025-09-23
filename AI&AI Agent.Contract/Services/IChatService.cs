using AI_AI_Agent.Contract.DTOs;
using System.Collections.Generic;
using System.Threading;
using System;

namespace AI_AI_Agent.Contract.Services
{
    public interface IChatService
    {
        // public Task<string> GetStreamingChatMessage(ChatRequestDto request);
        //public Task<IEnumerable<MessageDto>> GetMessagesByChatUId(Guid uId);
        public Task<ChatDto> CreateChatAsync(string UserId);
        public Task<IEnumerable<ChatDto>> GetChatsByUserIdAsync(string UserId);
        IAsyncEnumerable<string> StreamChatAsync(ChatRequestDto request, string userId);
        public Task<ChatDto> GetChatByUIdAsync(Guid uId, string userId);
        public Task<bool> DeleteChatAsync(Guid uId, string userId);
        public Task<bool> RenameChatAsync(Guid uId, string newTitle, string userId);
        IAsyncEnumerable<string> StreamWebSearchAsync(WebSearchRequestDto request, string userId, CancellationToken ct = default);
    }
}
