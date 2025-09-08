using AI_AI_Agent.Domain.Entities.Enums;

namespace AI_AI_Agent.Domain.Entities
{
    public class Message : BaseEntity
    {
        public string Content { get; set; }
        public LanguageModel Languages { get; set; }
        public DateTime TimeStamp { get; set; }
        public MessageRole Roles { get; set; }
        public int TotalToken { get; set; }
        public int InputToken { get; set; }
        public int OutputToken { get; set; }

        public Guid ChatId { get; set; }

    }
}
