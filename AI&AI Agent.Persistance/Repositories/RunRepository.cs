using AI_AI_Agent.Domain.Repositories;
using AI_AI_Agent.Persistance.AppDbContext;

namespace AI_AI_Agent.Persistance.Repositories;

public class RunRepository : GenericRepository<Domain.Entities.Run>, IRunRepository
{
    public RunRepository(AIDbContext context) : base(context)
    {
    }
}