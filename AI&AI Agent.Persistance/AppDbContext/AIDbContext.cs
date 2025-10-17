using AI_AI_Agent.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AI_AI_Agent.Persistance.AppDbContext
{
    public class AIDbContext : IdentityDbContext
    {
        public AIDbContext(DbContextOptions<AIDbContext> options): base(options) { }
        public DbSet<Chat> Chats { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Run> Runs { get; set; }
        public DbSet<StepLog> StepLogs { get; set; }
        public DbSet<Artifact> Artifacts { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Run>(entity =>
            {
                entity.HasMany(e => e.StepLogs)
                    .WithOne(e => e.Run)
                    .HasForeignKey(e => e.RunId);

                entity.HasMany(e => e.Artifacts)
                    .WithOne(e => e.Run)
                    .HasForeignKey(e => e.RunId);

                entity.Property(e => e.TotalCost).HasPrecision(18, 4);
            });

            builder.Entity<StepLog>()
                .HasIndex(s => new { s.RunId, s.CreatedAt });

            builder.Entity<Artifact>()
                .HasIndex(a => a.RunId);
        }
    }
}
