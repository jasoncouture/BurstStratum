using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BurstPool.Database;
using BurstPool.Database.Models;
using BurstPool.Models;
using BurstPool.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StrangeSoft.Burst;

namespace BurstPool.Services
{
    public class BlockStateTracker : BackgroundJob
    {
        private IBlockHeightTracker BlockHeightTracker { get; }
        private ILogger Logger { get; }
        private IServiceScopeFactory ServiceScopeFactory { get; }
        private IBurstApi BurstApi { get; }
        private IConfiguration Configuration { get; }
        public BlockStateTracker(IBlockHeightTracker blockHeightTracker, IBurstApi burstApi, IServiceScopeFactory serviceScopeFactory, ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            BlockHeightTracker = blockHeightTracker;
            BlockHeightTracker.MiningInfoChanged += OnMiningInfoChanged;
            ServiceScopeFactory = serviceScopeFactory;
            Logger = loggerFactory.CreateLogger<BlockStateTracker>();
            BurstApi = burstApi;
            Configuration = configuration;
        }
        ManualResetEventSlim _resetEvent = new ManualResetEventSlim(true);
        private void OnMiningInfoChanged(object sender, EventArgs e)
        {
            lock (_resetEvent)
            {
                if (!_resetEvent.IsSet)
                    _resetEvent.Set();
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stopCancellationToken)
        {
            while (!stopCancellationToken.IsCancellationRequested)
            {
                try
                {

                    var currentBlock = await BlockHeightTracker.GetCurrentBlockHeightAsync(stopCancellationToken).ConfigureAwait(false);
                    if (currentBlock == null || !long.TryParse(currentBlock.Height, out var height) || height <= 0) continue;
                    height = height - 1;
                    long nextBlockHeight = 0;
                    using (var scope = ServiceScopeFactory.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<PoolContext>();
                        var nextBlock = await db.Blocks.Where(i => !db.BlockStates.Any(x => x.Height == i.Height) && i.Height < height).OrderBy(i => i.Height).FirstOrDefaultAsync(stopCancellationToken);
                        if (nextBlock != null)
                        {
                            nextBlockHeight = nextBlock.Height;
                        }
                    }
                    if (nextBlockHeight > 0)
                    {
                        await ProcessBlockStateAsync(nextBlockHeight, stopCancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        lock (_resetEvent)
                            if (_resetEvent.IsSet)
                                _resetEvent.Reset();
                        while (!_resetEvent.IsSet)
                        {
                            stopCancellationToken.ThrowIfCancellationRequested();
                            if (_resetEvent.Wait(10000))
                                break;
                        }
                    }
                }
                catch (OperationCanceledException) when (stopCancellationToken.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "An unhandled exception occured while trying to update block state, will retry in 30 seconds.");
                    await Task.Delay(TimeSpan.FromSeconds(30), stopCancellationToken).ConfigureAwait(false);
                }
            }
        }
        private Tuple<string, AccountAddress> _address;
        private async Task<AccountAddress> GetPoolAccountAddressAsync(CancellationToken cancellationToken)
        {
            var poolAddress = Configuration.GetSection("Pool").GetValue<string>("Address");
            if (string.IsNullOrWhiteSpace(poolAddress)) return null;
            var cachedAddress = _address;
            if (cachedAddress != null && cachedAddress.Item1 == poolAddress)
            {
                return cachedAddress.Item2;
            }

            cachedAddress = Tuple.Create(poolAddress, await BurstApi.GetAccountAddressAsync(poolAddress, cancellationToken).ConfigureAwait(false));
            _address = cachedAddress;
            return cachedAddress.Item2;
        }
        private async Task ProcessBlockStateAsync(long blockHeight, CancellationToken stopCancellationToken)
        {
            
            AccountAddress poolAddress = null;
            bool first = true;
            while (poolAddress == null)
            {
                if (!first)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stopCancellationToken);
                }
                else
                {
                    first = false;
                }
                poolAddress = await GetPoolAccountAddressAsync(stopCancellationToken);

            }
            var blockResult = await BurstApi.GetBlockAsync(blockHeight, stopCancellationToken).ConfigureAwait(false);
            if (blockResult == null) return;
            var rewardReceipient = await BurstApi.GetRewardReceipientAsync(blockResult.Generator.ToString(), stopCancellationToken);
            var rewardReceipientAccount = await BurstApi.GetAccountAddressAsync(rewardReceipient);
            bool poolWins = rewardReceipientAccount.Account == poolAddress.Account;
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PoolContext>();
                using (var transaction = await db.Database.BeginTransactionAsync(stopCancellationToken))
                {
                    if(await db.BlockStates.AnyAsync(i => i.Height == blockHeight)) return;
                    var blockState = new BlockState()
                    {
                        Height = blockHeight,
                        IsPoolMember = poolWins,
                    };
                    if (poolWins)
                    {
                        var previousTransaction = await db.PoolTransactions.OrderByDescending(i => i.Height).ThenByDescending(i => i.Created).FirstOrDefaultAsync(stopCancellationToken);
                        var poolTransaction = db.PoolTransactions.Add(new PoolTransaction()
                        {
                            Adjustment = blockResult.TotalBlockReward,
                            Height = blockHeight,
                            PoolBalance = (previousTransaction?.PoolBalance ?? 0m) + blockResult.TotalBlockReward
                        });
                        await db.SaveChangesAsync(stopCancellationToken);
                        blockState.PoolTransactionId = poolTransaction.Entity.Id;
                    }
                    db.BlockStates.Add(blockState);
                    await db.SaveChangesAsync(stopCancellationToken);
                    transaction.Commit();
                    if (blockState.IsPoolMember)
                    {
                        Logger.LogInformation("Block {height}: Pool wins, Block reward: {reward}. Winner: {winner}", blockHeight, blockResult.TotalBlockReward, blockResult.GeneratorReedSolomon);
                    }
                    else
                    {
                        Logger.LogInformation("Block {height}: Pool didn't win.", blockHeight);
                    }
                }
            }

        }
    }
}