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
        
        // protected override void OnModelCreating(ModelBuilder modelBuilder)
        // {
        //     modelBuilder.Entity<Lock>()
        //         .ForSqlServerIsMemoryOptimized()
        //         .HasIndex(i => i.Resource);
        // }

    }
}