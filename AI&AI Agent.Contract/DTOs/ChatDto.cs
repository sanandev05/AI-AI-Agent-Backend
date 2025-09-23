using AI_AI_Agent.Domain.Entities.Enums;
using AI_AI_Agent.Domain.Entities;

namespace AI_AI_Agent.Contract.DTOs
{
    public class ChatDto
    {
        public Guid ChatGuid { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public ChatStatus Status { get; set; }
        public int TotalTokensConsumed { get; set; }
        public string? UserId { get; set; }
        public string? Title { get; set; }
        public ICollection<MessageDto> Messages { get; set; }
    }
}
