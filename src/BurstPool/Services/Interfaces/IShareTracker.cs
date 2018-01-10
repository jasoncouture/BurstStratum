using System.Threading.Tasks;

namespace BurstPool.Services.Interfaces
{
    public interface IShareTracker
    {
        Task RecordSharesAsync(ulong accountId, long block, decimal shares, ulong nonce, ulong deadline);
    }
}