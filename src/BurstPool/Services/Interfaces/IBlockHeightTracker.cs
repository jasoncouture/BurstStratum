using System.Threading;
using System.Threading.Tasks;
using BurstPool.Models;
namespace BurstPool.Services.Interfaces
{
    public interface IBlockHeightTracker
    {
         Task<MiningInfo> GetCurrentBlockHeightAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}