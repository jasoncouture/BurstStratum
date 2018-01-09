using System;
using System.Threading.Tasks;
using StrangeSoft.Burst;

namespace BurstStratum.Services.Interfaces {
    public interface IMiningInfoPoller {
        Task<MiningInfo> GetCurrentMiningInfoAsync(); 
        event EventHandler MiningInfoChanged;  
    }

}