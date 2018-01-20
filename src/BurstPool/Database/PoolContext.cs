using BurstPool.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace BurstPool.Database
{
    public class PoolContext : DbContext
    {
        public PoolContext(DbContextOptions<PoolContext> options) : base(options) { }
        public DbSet<Share> Shares { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<Block> Blocks { get; set; }
        public DbSet<Earnings> Earnings { get; set; }
        public DbSet<AccountBalance> AccountBalances { get; set; }
        public DbSet<AccountTransaction> AccountTransactions { get; set; }
        public DbSet<PoolTransaction> PoolTransactions { get; set; }
        public DbSet<BlockState> BlockStates { get; set; }
        public DbSet<AccountAverageShareHistory> AccountAverageShareHistory { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BlockState>()
                .HasIndex(i => i.Created);

            modelBuilder.Entity<PoolTransaction>()
                .HasIndex(i => i.Created);
        }

    }
}