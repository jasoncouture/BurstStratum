using System.Threading.Tasks;
using BurstPool.Models;

namespace BurstPool.Services.Interfaces
{
    public interface IBurstApi
    {

        Task<AccountAddress> GetAccountAddressAsync(string address);
        Task<AccountAddress> GetAccountAddressAsync(ulong accountId);
    }
}