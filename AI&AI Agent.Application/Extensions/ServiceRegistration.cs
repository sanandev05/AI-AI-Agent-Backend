using AI_AI_Agent.Application.Profiles;
using AI_AI_Agent.Application.Services;
using AI_AI_Agent.Application.Validators;
using AI_AI_Agent.Contract.Services;
using AI_AI_Agent.Domain.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AI_AI_Agent.Application.Extensions
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddServiceRegistration(this IServiceCollection services)
        {
            services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
            services.AddValidatorsFromAssemblyContaining<MessageValidator>();
            services.AddScoped(typeof(IGenericService<,>), typeof(GenericService<,>));
            return services;
        }
    }
}
