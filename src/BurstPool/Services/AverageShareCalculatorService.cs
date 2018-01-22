using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BurstPool.Database;
using BurstPool.Database.Models;
using BurstPool.Messages;
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
        private IMessenger Messenger { get; }

        public AverageShareCalculatorService(IBlockHeightTracker blockHeightTracker, IConfiguration configuration, ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory, IMessenger messenger)
        {
            Logger = loggerFactory.CreateLogger("Averager");
            Configuration = configuration;
            ScopeFactory = scopeFactory;
            BlockHeightTracker = blockHeightTracker;
            BlockHeightTracker.MiningInfoChanged += OnMiningInfoChanged;
            Messenger = messenger;
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
                    if (!_resetEvent.IsSet)
                    {
                        _resetEvent.Wait(TimeSpan.FromMinutes(1), stopCancellationToken);
                    }
                    var currentBlock = await BlockHeightTracker.GetCurrentBlockHeightAsync(stopCancellationToken);
                    var currentPassHeight = long.Parse(currentBlock.Height);
                    if (!await HandleCurrentPassAsync(currentPassHeight - 1, stopCancellationToken) && currentPassHeight == lastPassHeight)
                    {
                        var blockAfterPass = await BlockHeightTracker.GetCurrentBlockHeightAsync(stopCancellationToken);
                        if (blockAfterPass.Height == currentBlock.Height)
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
                long height = 0;
                using (var scope = ScopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<PoolContext>();
                    
                    using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
                    {
                        var nextBlock = await db.BlockStates.Where(i => !i.SharesAveraged && i.Height <= currentPassHeight).OrderBy(i => i.Height).FirstOrDefaultAsync();
                        if (nextBlock == null) return processedAny;
                        height = nextBlock.Height;
                        await ProcessBlockAsync(nextBlock.Height, db, cancellationToken).ConfigureAwait(false);
                        processedAny = true;
                        nextBlock.SharesAveraged = true;
                        await db.SaveChangesAsync(cancellationToken);
                        transaction.Commit();
                    }
                }
                await Messenger.PublishAsync("Block.State.Averaged", this, new BlockStateChangedMessage(height)).ConfigureAwait(false);
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

    }
}