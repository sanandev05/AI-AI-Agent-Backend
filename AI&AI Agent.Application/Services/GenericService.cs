using AI_AI_Agent.Contract.Services;
using AI_AI_Agent.Domain.Entities;
using AI_AI_Agent.Domain.Repositories;
using AutoMapper;

namespace AI_AI_Agent.Application.Services
{
    public class GenericService<TDto, TEntity> : IGenericService<TDto, TEntity> where TEntity : BaseEntity
        where TDto : class
    {
        private readonly IGenericRepository<TEntity> _repository;
        private readonly IMapper _mapper;

        public GenericService(IGenericRepository<TEntity> repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<TDto> AddAsync(TDto entity)
        {
            var map = _mapper.Map<TEntity>(entity);
            var add = await _repository.AddAsync(map);
            return entity;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }

        public async Task<IEnumerable<TDto>> GetAllAsync()
        {
            var getAll = await _repository.GetAllAsync();
            return _mapper.Map<IEnumerable<TDto>>(getAll);
        }

        public async Task<TDto> GetByIdAsync(int id)
        {
            var get = await _repository.GetByIdAsync(id);
            return _mapper.Map<TDto>(get);
        }

        public async Task<TDto> UpdateAsync(TDto entity)
        {
            var map = _mapper.Map<TEntity>(entity);
            await _repository.UpdateAsync(map);
            return entity;
        }
    }
}
