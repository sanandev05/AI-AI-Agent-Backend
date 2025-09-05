using AI_AI_Agent.Domain.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace AI_AI_Agent.Domain.Entities
{
    public class Chat
    {
        [Key]
        public Guid ChatGuid { get; set; } = Guid.NewGuid();
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; }
        public int TotalTokensConsumed { get; set; }
        public List<Message> Messages { get; set; } = new();

        public ChatStatus Status { get; set; } = ChatStatus.Active;

    }
}
