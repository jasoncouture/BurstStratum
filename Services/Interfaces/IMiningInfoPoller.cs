using System;
using System.Threading.Tasks;
using BurstStratum.Models;

namespace BurstStratum.Services.Interfaces {
    public interface IMiningInfoPoller {
        Task<MiningInfo> GetCurrentMiningInfoAsync(); 
        event EventHandler MiningInfoChanged;  
    }
}