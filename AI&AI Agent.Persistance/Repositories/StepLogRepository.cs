using AI_AI_Agent.Domain.Repositories;
using AI_AI_Agent.Persistance.AppDbContext;

namespace AI_AI_Agent.Persistance.Repositories;

public class StepLogRepository : GenericRepository<Domain.Entities.StepLog>, IStepLogRepository
{
    public StepLogRepository(AIDbContext context) : base(context)
    {
    }
}