using System.Collections.Generic;

namespace BurstStratum.Configuration
{

    public class StratumSettings
    {
        public List<string> Wallets { get; set; } = new List<string>();
        public double PollIntervalSeconds { get; set; } = 1;
        public ulong MaximumDeadline { get; set; }
    }
}