using AI_AI_Agent.Domain.Entities.Enums;

namespace AI_AI_Agent.Contract.DTOs
{
    public class MessageDto
    {
        public string Content { get; set; }
        public LanguageModel Language { get; set; }
        public DateTime TimeStamp { get; set; }
        public MessageRole Roles { get; set; }
        public int TotalToken { get; set; }
        public int InputToken { get; set; }
        public int OutputToken { get; set; }
        public int ChatId { get; set; }

    }
}
