using Newtonsoft.Json;

namespace BurstPool.Messages
{
    public class DeadlineMessage
    {
        public DeadlineMessage(ulong account, ulong nonce, long deadline, decimal shares)
        {
            Account = account;
            Nonce = nonce;
            Deadline = deadline;
            Shares = shares;
        }
        [JsonProperty("account")]
        public ulong Account { get; }
        [JsonProperty("nonce")]
        public ulong Nonce { get; }
        [JsonProperty("deadline")]
        public long Deadline { get; }
        [JsonProperty("shares")]
        public decimal Shares { get; }
    }
    public class BlockStateChangedMessage
    {
        public BlockStateChangedMessage(long height)
        {
            Height = height;
        }
        public long Height { get; }
    }
}