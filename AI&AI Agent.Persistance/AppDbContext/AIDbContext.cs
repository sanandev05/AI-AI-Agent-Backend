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
    }
}
