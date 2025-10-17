namespace AI_AI_Agent.Contract.DTOs
{
    public class ChatRequestDto
    {
        // Model preference from the client. Accepts either:
        // - a concrete model key (e.g., "gpt-4o", "gemini-2.5-flash")
        // - or a provider name ("OpenAI", "Google")
        // - or a numeric alias ("1" = OpenAI, "2" = Google) for backward compatibility
        public string? Model { get; set; }

        public string? Message { get; set; }
        public List<string>? ImageUrls { get; set; }
        public string? ChatId { get; set; }

        // Optional: select a concrete model by key (e.g., "gpt-4o", "gpt-3.5-turbo", "gemini-1.5-pro").
        // If provided, the backend will resolve this keyed model first; otherwise it will use the Model enum fallback.
        public string? ModelKey { get; set; }

        // Optional: provider hint (e.g., "OpenAI", "Google", "Anthropic"). Not required when ModelKey is set.
        public string? Provider { get; set; }
    }
}
