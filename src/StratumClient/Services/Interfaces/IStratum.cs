using System.Threading;
using System.Threading.Tasks;
using StrangeSoft.Burst;

namespace StratumClient.Services.Interfaces
{
    public interface IStratum
    {
         Task<MiningInfo> GetMiningInfoAsync(CancellationToken cancellationToken);
    }
}