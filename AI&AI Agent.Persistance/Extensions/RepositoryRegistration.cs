using AI_AI_Agent.Domain.Repositories;
using AI_AI_Agent.Persistance.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AI_AI_Agent.Persistance.Extensions
{
    public static class RepositoryRegistration
    {
        public static IServiceCollection AddRepositoryRegistration(this IServiceCollection services)
        {
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<IChatRepository, ChatRepository>();
            services.AddScoped<IRunRepository, RunRepository>();
            services.AddScoped<IStepLogRepository, StepLogRepository>();
            services.AddScoped<IArtifactRepository, ArtifactRepository>();
            return services;
        }
    }
}
