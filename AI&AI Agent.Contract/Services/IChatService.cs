using AI_AI_Agent.Contract.DTOs;
using AI_AI_Agent.Domain.Entities.Enums;

namespace AI_AI_Agent.Contract.Services
{
    public interface IChatService
    {
        // public Task<string> GetStreamingChatMessage(ChatRequestDto request);
        //public Task<IEnumerable<MessageDto>> GetMessagesByChatUId(Guid uId);
        public Task<ChatDto> CreateChatAsync(string UserId);
        public abstract Task<ChatDto> GetChatsByUserIdAsync(string UserId);
    }
}
