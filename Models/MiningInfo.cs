using System;
using Newtonsoft.Json;

namespace BurstStratum.Models
{
    public class MiningInfo : IComparable<MiningInfo>
    {
        [JsonProperty("generationSignature")]
        public string GenerationSignature { get; set; }
        [JsonProperty("baseTarget")]
        public string BaseTarget { get; set; }
        [JsonProperty("requestProcessingTime")]
        public ulong RequestProcessingTime { get; set; }
        [JsonProperty("height")]
        public string Height { get; set; }
        [JsonProperty("targetDeadline")]
        public ulong TargetDeadline { get; set; }
        
        int IComparable<MiningInfo>.CompareTo(MiningInfo other)
        {
            if(other == null) return -1;
            return ulong.Parse(Height).CompareTo(ulong.Parse(other.Height));
        }
    }
}