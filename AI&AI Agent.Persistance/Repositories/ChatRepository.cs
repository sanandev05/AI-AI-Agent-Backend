using AI_AI_Agent.Domain.Entities;
using AI_AI_Agent.Domain.Repositories;
using AI_AI_Agent.Persistance.AppDbContext;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AI_AI_Agent.Persistance.Repositories
{
    public class ChatRepository : IChatRepository
    {
        private readonly AIDbContext _context;
        private readonly DbSet<Chat> _dbSet;

        public ChatRepository(AIDbContext context)
        {
            _context = context;
            _dbSet = _context.Set<Chat>();
        }

        public async Task<Chat> AddAsync(Chat entity)
        {
            entity.CreatedAt = DateTime.Now;
            entity.UpdatedAt = DateTime.Now;
            var addToDb = await _dbSet.AddAsync(entity);
            await _context.SaveChangesAsync();
            return addToDb.Entity;
        }

        public async Task<Chat> UpdateAsync(Chat entity)
        {
            var existing = await _dbSet.FindAsync(entity.ChatGuid);
            if (existing == null) return null;

            entity.UpdatedAt = DateTime.Now;
            _context.Entry(existing).CurrentValues.SetValues(entity);

            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var chat = await _dbSet.FindAsync(id);
            if (chat == null) return false;

            chat.IsDeleted = true;
            chat.UpdatedAt = DateTime.Now;

            _dbSet.Update(chat);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Chat>> GetAllAsync(Expression<Func<Chat, bool>> filter = null, params Expression<Func<Chat, object>>[] includes)
        {
            IQueryable<Chat> query = _dbSet.AsNoTracking();

            if (filter != null)
            {
                query = query.Where(filter);
            }

            if (includes != null)
            {
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            }

            return await query.Where(c => !c.IsDeleted).ToListAsync();
        }

        public async Task<Chat> GetByIdAsync(Guid id, params Expression<Func<Chat, object>>[] includes)
        {
            IQueryable<Chat> query = _dbSet.AsNoTracking();

            if (includes != null)
            {
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            }

            return await query.FirstOrDefaultAsync(c => c.ChatGuid == id && !c.IsDeleted);
        }

        //Custom

        public async Task<List<Chat>> GetChatsByUserIdAsync(string userId, params Expression<Func<Chat, object>>[] includes)
        {
            IQueryable<Chat> query = _dbSet.AsNoTracking();

            if (includes != null)
            {
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            }

            return await query
                .Where(c => c.UserId == userId && !c.IsDeleted)
                .ToListAsync();
        }
    }
}
