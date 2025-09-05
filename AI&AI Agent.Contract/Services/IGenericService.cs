using AI_AI_Agent.Domain.Entities;
using AI_AI_Agent.Domain.Repositories;

namespace AI_AI_Agent.Contract.Services
{
    public interface IGenericService<TDto,TEntity> where TEntity : class where TDto : class
    {
        Task<TDto> GetByIdAsync(int id);
        Task<IEnumerable<TDto>> GetAllAsync();
        Task<TDto> AddAsync(TDto entity);
        Task<TDto> UpdateAsync(TDto entity);
        Task<bool> DeleteAsync(int id);
    }
}
