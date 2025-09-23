using System.Collections.Generic;

namespace AI_AI_Agent.Contract.DTOs
{
    public class WebSearchResult
    {
        public string? Title { get; set; }
        public string? Url { get; set; }
        public string? Snippet { get; set; }
    }

    public class WebSearchResultDto
    {
        public string? Query { get; set; }
        public List<WebSearchResult>? Results { get; set; }
        public string? Error { get; set; }
    }
}
