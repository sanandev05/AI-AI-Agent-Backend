using AI_AI_Agent.Domain.Entities;
using AI_AI_Agent.Domain.Repositories;
using AI_AI_Agent.Persistance.AppDbContext;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Linq.Expressions;

namespace AI_AI_Agent.Persistance.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
    {
        private readonly AIDbContext _context;
        private readonly DbSet<T> _dbSet;

        public GenericRepository(AIDbContext context)
        {
            _context = context;
            _dbSet = _context.Set<T>();
        }

        public async Task<T> AddAsync(T entity)
        {
            entity.CreatedAt = DateTime.Now;
            entity.UpdatedAt = DateTime.Now;
            var addToDb = await _dbSet.AddAsync(entity);
            await _context.SaveChangesAsync();
            return addToDb.Entity;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var getEntity = await _dbSet.FindAsync(id);
            if(getEntity is null) { return false; }
            getEntity.IsDeleted = true;
            _dbSet.Update(getEntity);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<T>> GetAllAsync(Expression<Func<T, bool>> filter = null, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _dbSet.AsNoTracking();

            if (filter != null)
            {
                query = query.Where(filter);
            }

            if (includes != null)
            {
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            }

            return await query.Where(x => !x.IsDeleted).ToListAsync();
        }

        public async Task<T> GetByIdAsync(int id, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _dbSet.AsNoTracking();

            if (includes != null)
            {
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            }

            return await query.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        }

        public async Task<T> UpdateAsync(T entity)
        {
            var getExistingData =await _dbSet.FindAsync(entity.Id);
            if (getExistingData == null) return null;

            entity.UpdatedAt = DateTime.Now;
            _dbSet.Update(entity);
            await _context.SaveChangesAsync();
            return entity;
        }
    }
}
