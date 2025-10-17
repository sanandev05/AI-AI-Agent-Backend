using AI_AI_Agent.Domain.Repositories;
using AI_AI_Agent.Persistance.AppDbContext;

namespace AI_AI_Agent.Persistance.Repositories;

public class ArtifactRepository : GenericRepository<Domain.Entities.Artifact>, IArtifactRepository
{
    public ArtifactRepository(AIDbContext context) : base(context)
    {
    }
}