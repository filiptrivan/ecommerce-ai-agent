using Microsoft.EntityFrameworkCore;
using EcommerceAIAgent.Business.Entities;
using Spiderly.Infrastructure;

namespace EcommerceAIAgent.Infrastructure
{
    public partial class EcommerceAIAgentApplicationDbContext : ApplicationDbContext<User> // https://stackoverflow.com/questions/41829229/how-do-i-implement-dbcontext-inheritance-for-multiple-databases-in-ef7-net-co
    {
        public EcommerceAIAgentApplicationDbContext(DbContextOptions<EcommerceAIAgentApplicationDbContext> options)
        : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await base.SaveChangesAsync(cancellationToken);
        }

    }
}
