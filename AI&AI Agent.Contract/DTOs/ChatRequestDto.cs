using AI_AI_Agent.Domain.Entities.Enums;

namespace AI_AI_Agent.Contract.DTOs
{
    public class ChatRequestDto
    {
        public string Message { get; set; }
        public LanguageModel LanguageModel { get; set; }
        public string ChatId { get; set; }
    }
}
