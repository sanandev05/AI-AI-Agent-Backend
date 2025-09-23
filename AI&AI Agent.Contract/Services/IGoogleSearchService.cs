using System.Threading.Tasks;
using AI_AI_Agent.Contract.DTOs;

namespace AI_AI_Agent.Contract.Services
{
    public interface IGoogleSearchService
    {
        Task<WebSearchResultDto> SearchAsync(string query);
    }
}