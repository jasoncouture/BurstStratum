using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BurstPool.Database;
using BurstPool.Database.Models;
using BurstPool.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using StrangeSoft.Burst;

namespace BurstPool.Services
{
    public class BlockHeightTracker : BackgroundJob, IBlockHeightTracker
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IShareCalculator _shareCalculator;
        private readonly IMessenger _messenger;

        public BlockHeightTracker(IConfiguration configuration, ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory, IShareCalculator shareCalculator, IMessenger messenger)
        {
            _configuration = configuration;
            _logger = loggerFactory.CreateLogger<BlockHeightTracker>();
            _scopeFactory = scopeFactory;
            _shareCalculator = shareCalculator;
            _messenger = messenger;
        }
        public async Task<MiningInfo> GetCurrentBlockHeightAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (MiningInfo == null)
                await completionSource.Task.ConfigureAwait(false);
            return MiningInfo;
        }
        private HttpClient _client = new HttpClient();
        private MiningInfo _miningInfo = null;
        private TaskCompletionSource<object> completionSource = new TaskCompletionSource<object>();
        public event EventHandler MiningInfoChanged;
        private async void OnMiningInfoChanged()
        {
            try
            {
                await Task.Factory.StartNew(() => MiningInfoChanged?.Invoke(this, EventArgs.Empty)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occured in a MiningInfoChanged event handler");
            }
        }
        public MiningInfo MiningInfo
        {
            get { return _miningInfo; }
            set
            {
                if (!long.TryParse(value?.Height, out _))
                    return;
                if (_miningInfo != null && _miningInfo.Height == value.Height && _miningInfo.GenerationSignature == value.GenerationSignature)
                    return;
                _miningInfo = value;
                completionSource.TrySetResult(null);
                OnMiningInfoChanged();
                _logger.LogInformation($"New block detected: {value.Height}");
            }
        }
        protected override async Task ExecuteAsync(CancellationToken stopCancellationToken)
        {
            _logger.LogInformation("Started block monitoring background job.");
            ulong lastBlock = 0;
            while (!stopCancellationToken.IsCancellationRequested)
            {

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stopCancellationToken).ConfigureAwait(false);
                    var walletUrl = _configuration.GetSection("Pool")?.GetValue<string>("TrustedWallet");
                    var uriBuilder = new UriBuilder(walletUrl);
                    uriBuilder.Path = "/burst";
                    uriBuilder.Query = "?requestType=getMiningInfo";
                    var response = await _client.GetAsync(uriBuilder.Uri, stopCancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    var miningInfo = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false)).ToObject<MiningInfo>();
                    if (ulong.TryParse(miningInfo.Height, out var height) && height > lastBlock)
                    {
                        await HandleMiningInfoUpdateAsync(miningInfo, stopCancellationToken).ConfigureAwait(false);
                        lastBlock = height;
                    }
                    MiningInfo = miningInfo;

                }
                catch (OperationCanceledException) when (stopCancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update mining information. Will retry.");
                    await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
            }
        }

        private async Task HandleMiningInfoUpdateAsync(MiningInfo miningInfo, CancellationToken cancellationToken)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<PoolContext>();
                if (!long.TryParse(miningInfo.Height, out var height)) return;
                if (!ulong.TryParse(miningInfo.BaseTarget, out var baseTarget)) return;
                using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken))
                {
                    if (await context.Blocks.AnyAsync(i => i.Height == height, cancellationToken).ConfigureAwait(false)) return;

                    var newBlock = context.Blocks.Add(new Block()
                    {
                        Height = height,
                        BaseTarget = baseTarget,
                        Difficulty = _shareCalculator.GetDifficulty(baseTarget)
                    }).Entity;
                    _logger.LogInformation($"Adding block {newBlock.Height} with difficulty {newBlock.Difficulty}");
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    transaction.Commit();
                    _logger.LogInformation($"Added block {newBlock.Height} with difficulty {newBlock.Difficulty}");
                    _messenger.Publish("Public.Block.Update", this, miningInfo);
                }
            }
        }
    }
}