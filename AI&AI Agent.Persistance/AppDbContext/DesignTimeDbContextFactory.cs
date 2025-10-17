using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace AI_AI_Agent.Persistance.AppDbContext
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AIDbContext>
    {
        public AIDbContext CreateDbContext(string[] args)
        {
            // Get the path to the API project's directory
            var apiProjectPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "AI&AI Agent.API");

            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(apiProjectPath)
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddUserSecrets<DesignTimeDbContextFactory>(optional:true)
                .Build();

            var builder = new DbContextOptionsBuilder<AIDbContext>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            builder.UseSqlServer(connectionString);

            return new AIDbContext(builder.Options);
        }
    }
}
