using AI_AI_Agent.Domain.Entities.Enums;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace AI_AI_Agent.Domain.Entities
{
    public class Chat
    {
        [Key]
        public Guid ChatGuid { get; set; }
        public string UserId { get; set; }
        public IdentityUser User { get; set; }
        public ICollection<Message> Messages { get; set; }
        public ChatStatus Status { get; set; }
        public int TotalTokensConsumed { get; set; }
        public string? Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
    }
}
