using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BurstPool.Database;
using BurstPool.Database.Models;
using BurstPool.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StrangeSoft.Burst;

namespace BurstPool.Services
{
    public class AverageShareCalculatorService : BackgroundJob
    {
        private ILogger Logger { get; }
        private IServiceScopeFactory ScopeFactory { get; }
        private IConfiguration Configuration { get; }
        private IBlockHeightTracker BlockHeightTracker { get; }
        public AverageShareCalculatorService(IBlockHeightTracker blockHeightTracker, IConfiguration configuration, ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory)
        {
            Logger = loggerFactory.CreateLogger("Averager");
            Configuration = configuration;
            ScopeFactory = scopeFactory;
            BlockHeightTracker = blockHeightTracker;
            BlockHeightTracker.MiningInfoChanged += OnMiningInfoChanged;
        }
        ManualResetEventSlim _resetEvent = new ManualResetEventSlim(true);
        private void OnMiningInfoChanged(object sender, EventArgs e)
        {
            lock (_resetEvent)
                if (!_resetEvent.IsSet)
                    _resetEvent.Set();
        }

        protected override async Task ExecuteAsync(CancellationToken stopCancellationToken)
        {
            long lastPassHeight = 0;
            while (!stopCancellationToken.IsCancellationRequested)
            {
                try
                {
                    while (!_resetEvent.IsSet)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), stopCancellationToken);
                    }
                    var currentBlock = await BlockHeightTracker.GetCurrentBlockHeightAsync(stopCancellationToken);
                    var currentPassHeight = long.Parse(currentBlock.Height);
                    if (!await HandleCurrentPassAsync(currentPassHeight - 2, stopCancellationToken) && currentPassHeight == lastPassHeight)
                    {
                        lock (_resetEvent)
                            if (_resetEvent.IsSet)
                                _resetEvent.Reset();
                    }
                    lastPassHeight = currentPassHeight;
                }
                catch (OperationCanceledException) when (stopCancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "An unhandled exception occured processing account history, will try again");
                }

            }
        }

        private async Task<bool> HandleCurrentPassAsync(long currentPassHeight, CancellationToken cancellationToken)
        {

            bool processedAny = false;

            while (true)
            {
                using (var scope = ScopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<PoolContext>();
                    var nextBlock = await db.BlockStates.Where(i => !i.SharesAveraged && i.Height <= currentPassHeight).OrderBy(i => i.Height).FirstOrDefaultAsync();
                    if (nextBlock == null) return processedAny;
                    using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
                    {
                        await ProcessBlockAsync(nextBlock.Height, db, cancellationToken).ConfigureAwait(false);
                        processedAny = true;
                        nextBlock.SharesAveraged = true;
                        await db.SaveChangesAsync(cancellationToken);
                        transaction.Commit();
                    }
                }
            }
        }
        private int Period => Configuration.GetSection("Pool").GetValue<int>("SMAPeriod");
        private async Task ProcessBlockAsync(long height, PoolContext db, CancellationToken cancellationToken)
        {
            var period = Period;
            var oldestHeight = height - period - 1;
            var accounts = await db.Accounts.Where(i => i.Shares.Any(x => x.BlockId <= height && x.BlockId > oldestHeight) && !i.AverageShareHistory.Any(x => x.Height == height)).Select(i => i.Id).ToListAsync(cancellationToken);
            int counter = 0;
            foreach (var account in accounts)
            {

                if (await db.AccountAverageShareHistory.AnyAsync(x => x.AccountId == account && x.Height == height, cancellationToken)) continue;
                var shares = await db.Shares.Where(i => i.AccountId == account && i.BlockId <= height && i.BlockId > oldestHeight).Select(i => new { i.BlockId, i.ShareValue }).ToListAsync(cancellationToken);
                var average = shares.GroupBy(i => i.BlockId).Select(x => x.Max(t => t.ShareValue)).Sum() / period;
                Logger.LogInformation("Account {accountId}, computed average shares {average} for block {blockHeight}", account, average, height);
                db.AccountAverageShareHistory.Add(new AccountAverageShareHistory()
                {
                    AccountId = account,
                    Height = height,
                    AverageShares = average
                });
                counter += 1;
                // Save every 1000 inserts, to avoid performance hit.
                if (counter % 1000 == 0)
                    await db.SaveChangesAsync(cancellationToken);
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        private async Task ProcessSingleAccountAsync(ulong account, long passHeight, PoolContext db, CancellationToken cancellationToken)
        {
            var lastHeight = 0L;
            var lastAverageEntry = await db.AccountAverageShareHistory.Where(i => i.AccountId == account && i.Height <= passHeight).OrderByDescending(i => i.Height).FirstOrDefaultAsync(cancellationToken);
            var lastComputedBlockState = await db.BlockStates.Where(i => i.Height <= passHeight && !i.SharesAveraged).Select(i => i.Height).ToListAsync(cancellationToken);
        }
    }
}