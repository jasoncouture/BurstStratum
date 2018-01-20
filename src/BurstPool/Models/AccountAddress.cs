using Newtonsoft.Json;

namespace BurstPool.Models
{
    public class BlockDetails
    {
        [JsonProperty("height")]
        public long Height {get;set;}
        [JsonProperty("generator")]
        public ulong Generator {get;set;}
        [JsonProperty("generatorRS")]
        public string GeneratorReedSolomon { get; set; }
        [JsonProperty("blockReward")]
        public decimal BlockReward { get; set; }
        [JsonProperty("totalFeeNQT")]
        public string TotalFeeNQT { get; set; }
        [JsonIgnore]
        public decimal TotalFee => ulong.TryParse(TotalFeeNQT, out var totalFee) ? (totalFee / 100000000m) : 0m;
        [JsonIgnore]
        public decimal TotalBlockReward => BlockReward + TotalFee;
    }
    public class AccountAddress
    {
        [JsonProperty("accountRS")]
        public string ReedSolomonAddress { get; set; }
        [JsonProperty("account")]
        public ulong Account { get; set; }
    }
}