using AI_AI_Agent.Domain.Entities;
using System.Linq.Expressions;

namespace AI_AI_Agent.Domain.Repositories
{
    public  interface IChatRepository
    {
        Task<List<Chat>> GetAllAsync(Expression<Func<Chat, bool>> filter = null, params Expression<Func<Chat, object>>[] includes);
        Task<Chat> GetByIdAsync(Guid id, params Expression<Func<Chat, object>>[] includes);
        Task<Chat> AddAsync(Chat entity);
        Task<Chat> UpdateAsync(Chat entity);
        Task<bool> DeleteAsync(Guid id);

        //Custom Methods
        Task<List<Chat>> GetChatsByUserIdAsync(string userId, params Expression<Func<Chat, object>>[] includes);

    }
}
