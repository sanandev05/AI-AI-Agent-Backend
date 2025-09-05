using AI_AI_Agent.Domain.Entities.Enums;
using AI_AI_Agent.Domain.Entities;

namespace AI_AI_Agent.Contract.DTOs
{
    public class ChatDto
    {
        public string UserId { get; set; }
        public string Title { get; set; }
        public int TotalTokensConsumed { get; set; }
        public List<Message> Messages { get; set; } = new();
        public ChatStatus Status { get; set; } = ChatStatus.Active;

    }
}
