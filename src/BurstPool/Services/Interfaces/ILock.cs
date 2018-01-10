using System;
using System.Threading;
using System.Threading.Tasks;

namespace BurstPool.Services.Interfaces
{
    public interface ILock : IDisposable {
        Task<bool> AssertLockHeldAsync(CancellationToken cancellationToken);
    }
}