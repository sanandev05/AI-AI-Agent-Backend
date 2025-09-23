using AI_AI_Agent.Domain.Entities.Enums;

namespace AI_AI_Agent.Contract.DTOs
{
    public class ChatRequestDto
    {
        public LanguageModel Model { get; set; }

        public string? Message { get; set; }
        public List<string>? ImageUrls { get; set; }
        public string? ChatId { get; set; }
    }
}
