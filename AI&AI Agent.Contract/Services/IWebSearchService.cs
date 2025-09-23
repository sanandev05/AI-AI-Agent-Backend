using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Contract.DTOs;

namespace AI_AI_Agent.Contract.Services
{
    public interface IWebSearchService
    {
        Task<WebSearchResultDto> SearchAsync(string query, CancellationToken ct = default);
        IAsyncEnumerable<string> StreamSearchAsync(string query, CancellationToken ct = default);
    }
}
