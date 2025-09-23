using AI_AI_Agent.Domain.Entities.Enums;

namespace AI_AI_Agent.Contract.DTOs
{
    public class MessageDto
    {
        public Guid ChatId { get; set; }
        public MessageRole Roles { get; set; }
        public string? Content { get; set; }
        public int? InputToken { get; set; }
        public int? OutputToken { get; set; }
        public int? TotalToken { get; set; }
        public List<string>? ImageUrls { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}
