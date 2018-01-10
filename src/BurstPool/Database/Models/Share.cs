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
    }

    public class Block
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None), Key]
        public long Height { get; set; }
        public ulong BaseTarget { get; set; }
        public decimal Difficulty { get; set; }
    }
}