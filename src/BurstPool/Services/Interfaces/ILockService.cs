using System.Threading;
using System.Threading.Tasks;

namespace BurstPool.Services.Interfaces
{
    public interface ILockService {
        Task<ILock> AcquireLockAsync(string resource, CancellationToken cancellationToken);
    }
}