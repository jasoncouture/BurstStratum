using System;
using System.Threading;
using System.Threading.Tasks;
using StrangeSoft.Burst;

namespace BurstPool.Services.Interfaces
{
    public interface IBlockHeightTracker
    {
        event EventHandler MiningInfoChanged;
        Task<MiningInfo> GetCurrentBlockHeightAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}