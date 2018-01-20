using System.Threading;
using System.Threading.Tasks;
using BurstPool.Models;

namespace BurstPool.Services.Interfaces
{
    public interface IBurstApi
    {
        Task<string> GetRewardReceipientAsync(string account, CancellationToken cancellationToken = default(CancellationToken));
        Task<BlockDetails> GetBlockAsync(long height, CancellationToken cancellationToken = default(CancellationToken));
        Task<AccountAddress> GetAccountAddressAsync(string address, CancellationToken cancellationToken = default(CancellationToken));
        Task<AccountAddress> GetAccountAddressAsync(ulong accountId, CancellationToken cancellationToken = default(CancellationToken));
    }
}