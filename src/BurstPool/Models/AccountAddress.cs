using Newtonsoft.Json;

namespace BurstPool.Models
{
    public class AccountAddress
    {
        [JsonProperty("accountRS")]
        public string ReedSolomonAddress { get; set; }
        [JsonProperty("account")]
        public ulong Account { get; set; }
    }
}