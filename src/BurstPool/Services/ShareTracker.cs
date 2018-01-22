using System.Threading.Tasks;
using BurstPool.Database;
using BurstPool.Database.Models;
using BurstPool.Messages;
using BurstPool.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BurstPool.Services
{
    // public class DistributedLock : ILock
    // {
    //     private readonly Func<string, CancellationToken, Task<bool>> _lockHeldCallback;
    //     private readonly Action<string> _lockReleaseCallback;
    //     private readonly string _lockId;
    //     private bool _disposed = false;

    //     public DistributedLock(string lockId, Func<string, CancellationToken, Task<bool>> lockHeldCheckCallback, Action<string> releaseLockCallback)
    //     {
    //         _lockHeldCallback = lockHeldCheckCallback;
    //         _lockReleaseCallback = releaseLockCallback;
    //         _lockId = lockId;
    //     }
    //     public async Task<bool> AssertLockHeldAsync(CancellationToken cancellationToken)
    //     {
    //         if (_disposed) return false;
    //         var result = await _lockHeldCallback(_lockId, cancellationToken).ConfigureAwait(false);
    //         return result;
    //     }

    //     public void Dispose()
    //     {
    //         if (_disposed) return;
    //         _disposed = true;
    //         _lockReleaseCallback(_lockId);
    //     }
    // }
    // public class DistrubtedLockService : BackgroundJob, ILockService
    // {
    //     private readonly ILogger _logger;
    //     private readonly IServiceScopeFactory _scopeFactory;
    //     private readonly PoolContext _context;
    //     private readonly string _instanceId;

    //     public DistrubtedLockService(ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory, IServerInstanceIdService instanceIdService)
    //     {
    //         _logger = loggerFactory.CreateLogger("LockService");
    //         _scopeFactory = scopeFactory;
    //         _instanceId = instanceIdService.InstanceId;
    //     }
    //     private readonly HashSet<string> _heldLocks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    //     ConditionalWeakTable<string, ILock> _lockCache = new ConditionalWeakTable<string, ILock>();

    //     public async Task<ILock> AcquireLockAsync(string resource, CancellationToken cancellationToken)
    //     {
    //         while (true)
    //         {
    //             var @lock = await TryAcquireLockAsync(resource, cancellationToken);
    //             if (@lock != null) return @lock;
    //             cancellationToken.ThrowIfCancellationRequested();
    //         }
    //     }

    //     private async Task<ILock> TryAcquireLockAsync(string resource, CancellationToken cancellationToken)
    //     {
    //         using (var scope = _scopeFactory.CreateScope())
    //         using (var context = scope.ServiceProvider.GetRequiredService<PoolContext>())
    //         {
    //             try
    //             {
    //                 string lockId = null;
    //                 using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
    //                 {
    //                     var now = DateTimeOffset.Now;
    //                     if (await context.Locks.AnyAsync(i => i.Expires > now && i.Resource == resource)) return null;
    //                     lockId = context.Locks.Add(new Lock()
    //                     {
    //                         ServerInstanceId = _instanceId,
    //                         Resource = resource
    //                     }).Entity.Id;
    //                     await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    //                     transaction.Commit();

    //                 }
    //                 using (var transaction = await context.Database.BeginTransactionAsync())
    //                 {
    //                     var lockRecord = await context.Locks.FindAsync(lockId);
    //                     if (await context.Locks.AsNoTracking().AnyAsync(i => i.Id != lockRecord.Id && (i.Acquired < lockRecord.Acquired || i.Expires > lockRecord.Expires)))
    //                     {
    //                         context.Entry(lockRecord).State = EntityState.Deleted;
    //                         lockRecord = null;
    //                         lockId = null;
    //                     }
    //                     else
    //                     {
    //                         foreach (var expiredLock in await context.Locks.Where(i => i.Expires < DateTimeOffset.Now).Take(20).ToListAsync())
    //                         {
    //                             context.Entry(expiredLock).State = EntityState.Deleted;
    //                         }
    //                     }
    //                     if (lockRecord != null)
    //                         lockRecord.Expires = DateTimeOffset.Now.AddSeconds(30);
    //                     await context.SaveChangesAsync();
    //                     transaction.Commit();
    //                     if (string.IsNullOrWhiteSpace(lockId)) return null;
    //                     return CreateDistributedLockObject(lockId);
    //                 }
    //             }
    //             catch
    //             {
    //                 return null;
    //             }
    //         }
    //     }

    //     private ILock CreateDistributedLockObject(string lockId)
    //     {
    //         lock (_lockCache)
    //         {
    //             var distributedLock = new DistributedLock(lockId, OnCheckLockRequested, OnReleaseLock);
    //             _heldLocks.Add(lockId);
    //             _lockCache.AddOrUpdate(lockId, distributedLock);
    //             return distributedLock;
    //         }
    //     }

    //     private async Task<bool> OnCheckLockRequested(string lockId, CancellationToken cancellationToken)
    //     {
    //         ILock distributedLock = null;
    //         lock (_lockCache)
    //             if (!_lockCache.TryGetValue(lockId, out distributedLock)) return false;
    //         using (var scope = _scopeFactory.CreateScope())
    //         {
    //             var context = scope.ServiceProvider.GetRequiredService<PoolContext>();
    //             using (var transaction = await context.Database.BeginTransactionAsync())
    //             {
    //                 var lockRecord = await context.Locks.FindAsync(lockId).ConfigureAwait(false);
    //                 if (lockRecord == null)
    //                 {
    //                     lock (_lockCache)
    //                         _lockCache.Remove(lockId);
    //                     return false;
    //                 }
    //                 if (await context.Locks.AnyAsync(i => i.Id != lockId && i.Resource == lockRecord.Resource && i.Expires > lockRecord.Expires))
    //                 {
    //                     context.Entry(lockRecord).State = EntityState.Deleted;
    //                     await context.SaveChangesAsync();
    //                     transaction.Commit();
    //                     OnReleaseLock(lockId);
    //                     return false;
    //                 }
    //                 else
    //                 {
    //                     lockRecord.Expires = DateTimeOffset.Now.AddSeconds(30);
    //                     await context.SaveChangesAsync(cancellationToken);
    //                     transaction.Commit();
    //                     return true;
    //                 }
    //             }
    //         }
    //     }

    //     private void OnReleaseLock(string lockId)
    //     {
    //         try
    //         {
    //             for (var x = 0; x < 3; x++)
    //             {
    //                 try
    //                 {
    //                     using (var scope = _scopeFactory.CreateScope())
    //                     {
    //                         var context = scope.ServiceProvider.GetRequiredService<PoolContext>();
    //                         var lockRecord = context.Locks.Find(lockId);
    //                         if (lockRecord == null) return;
    //                         context.Entry(lockRecord).State = EntityState.Deleted;
    //                         context.SaveChanges();
    //                         return;
    //                     }
    //                 }
    //                 catch (Exception ex)
    //                 {
    //                     _logger.LogWarning($"Failed to release lock {lockId}, will retry in 0.5s");
    //                     Thread.Sleep(500);
    //                 }
    //             }
    //             _logger.LogError($"Failed to release lock: {lockId}, will wait for lock to expire instead.");
    //         }
    //         finally
    //         {
    //             lock (_lockCache)
    //             {
    //                 _lockCache.Remove(lockId);
    //             }
    //         }
    //     }

    //     protected override async Task ExecuteAsync(CancellationToken stopCancellationToken)
    //     {
    //         _logger.LogInformation("Started distributed lock management background job.");
    //         while(!stopCancellationToken.IsCancellationRequested) {

    //         }
    //     }
    // }
    // public class ServerInstanceIdService : IServerInstanceIdService
    // {
    //     string _instanceId;
    //     public ServerInstanceIdService(ILoggerFactory loggerFactory)
    //     {
    //         _instanceId = string.Intern($"{Environment.MachineName}:{Guid.NewGuid():n}");
    //         loggerFactory.CreateLogger("Startup").LogInformation($"Started server instance: {_instanceId}");
    //     }

    //     public string InstanceId => _instanceId;
    // }
    public class ShareTracker : IShareTracker
    {
        private readonly PoolContext _context;
        private readonly IMessenger _messenger;
        public ShareTracker(PoolContext context, IMessenger messenger)
        {
            _context = context;
            _messenger = messenger;
        }
        public async Task RecordSharesAsync(ulong accountId, long block, decimal shares, ulong nonce, ulong deadline)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                if (await _context.Shares.AnyAsync(i => i.AccountId == accountId && i.BlockId == block && i.ShareValue >= shares))
                {
                    return;
                }
                if (!await _context.Accounts.AnyAsync(i => i.Id == accountId))
                {
                    _context.Accounts.Add(new Account()
                    {
                        Id = accountId
                    });
                    await _context.SaveChangesAsync().ConfigureAwait(false);
                }

                _context.Shares.Add(new Share()
                {
                    AccountId = accountId,
                    BlockId = block,
                    ShareValue = shares,
                    Nonce = nonce,
                    Deadline = deadline
                });
                await _context.SaveChangesAsync().ConfigureAwait(false);
                transaction.Commit();
            }
            var message = new DeadlineMessage(accountId, nonce, (long)deadline, shares);
            await _messenger.PublishAsync($"Public.Share.Block.Accepted.{block}", data: message);
            await _messenger.PublishAsync($"Public.Share.Account.Accepted.{accountId}", data: message);
        }
    }
}