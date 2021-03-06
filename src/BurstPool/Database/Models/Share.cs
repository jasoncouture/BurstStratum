using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BurstPool.Database.Models
{
    public class Earnings
    {
        [Key, ForeignKey(nameof(Block)), DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Height { get; set; }
        public decimal Amount { get; set; }
    }

    public class AccountBalance
    {
        [Key, ForeignKey(nameof(Account)), DatabaseGenerated(DatabaseGeneratedOption.None)]
        public ulong AccountId { get; set; }
        public virtual Account Account { get; set; }
        public decimal PendingBalance { get; set; }
        public virtual List<AccountTransaction> Transactions { get; set; } = new List<AccountTransaction>();
    }

    public class AccountTransaction
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string Id { get; set; } = Guid.NewGuid().ToString("n");
        [ForeignKey(nameof(Account))]
        public ulong AccountId { get; set; }
        public virtual Account Account { get; set; }
        [ForeignKey(nameof(Block))]
        public long BlockId { get; set; }
        public virtual Block Block { get; set; }
        public decimal Adjustment { get; set; }
    }

    public class Share
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None), Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("n");
        [ForeignKey(nameof(Block))]
        public long BlockId { get; set; }
        public virtual Block Block { get; set; }
        [ForeignKey(nameof(Account))]
        public ulong AccountId { get; set; }
        public virtual Account Account { get; set; }
        public ulong Nonce { get; set; }
        public ulong Deadline { get; set; }
        public decimal ShareValue { get; set; }
        public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
    }

    public class Account
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public ulong Id { get; set; }
        public virtual List<Share> Shares { get; set; } = new List<Share>();
        public virtual List<AccountAverageShareHistory> AverageShareHistory { get; set; } = new List<AccountAverageShareHistory>();
    }

    public class AccountAverageShareHistory
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string Id { get; set; } = Guid.NewGuid().ToString("n");
        public decimal AverageShares { get; set; }
        [ForeignKey(nameof(Block))]
        public long Height { get; set; }
        public virtual Block Block { get; set; }

        [ForeignKey(nameof(Account))]
        public ulong AccountId { get; set; }
        public virtual Account Account { get; set; }

        public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
    }

    public class Block
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None), Key]
        public long Height { get; set; }
        public ulong BaseTarget { get; set; }
        public decimal Difficulty { get; set; }
        public virtual List<PoolTransaction> PoolTransactions { get; set; }
        [InverseProperty("Block")]
        public virtual BlockState BlockState { get; set; }
    }
    public class PoolTransaction
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string Id { get; set; } = Guid.NewGuid().ToString("n");
        public decimal Adjustment { get; set; }
        public decimal PoolBalance { get; set; }
        [ForeignKey(nameof(Block))]
        public long Height { get; set; }
        public virtual Block Block { get; set; }
        public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
    }
    public class BlockState
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None), ForeignKey(nameof(Block))]
        public long Height { get; set; }
        public virtual Block Block { get; set; }
        public ulong Winner { get; set; }
        public bool IsPoolMember { get; set; }
        [ForeignKey(nameof(PoolTransaction))]
        public string PoolTransactionId { get; set; }
        public PoolTransaction PoolTransaction { get; set; }
        public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
        public bool SharesAveraged { get; set; }
        public bool PaymentsCalculated { get; set; }
    }
}