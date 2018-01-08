using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BurstStratum.Configuration;
using BurstStratum.Models;
using BurstStratum.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using StrangeSoft.Burst;
using ResponseTuple = System.Tuple<string, System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage>>;
namespace BurstStratum.Services
{
    public class MiningInfoPoller : BackgroundJob, IMiningInfoPoller
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        public event EventHandler MiningInfoChanged;

        private void OnMiningInfoChanged()
        {
            var miningInfoChanged = MiningInfoChanged;
            Task.Factory.StartNew(() =>
            {
                try { MiningInfoChanged?.Invoke(this, EventArgs.Empty); }
                catch
                {
                    // Ignored
                }
            });
        }
        public MiningInfoPoller(IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<MiningInfoPoller>();
            _configuration = configuration;
            _client = new HttpClient();
            _client.DefaultRequestHeaders.TryAddWithoutValidation("X-Info", "StratumProxy");
        }
        List<ResponseTuple> _workQueue = new List<ResponseTuple>();
        private MiningInfo _currentInfo;
        public MiningInfo CurrentInfo
        {
            get => _currentInfo;
            private set
            {
                _currentInfo = value;
                firstMiningInfoSetCompletionSource.TrySetResult(null);
                OnMiningInfoChanged();
            }
        }
        public async Task<MiningInfo> GetCurrentMiningInfoAsync()
        {
            if (CurrentInfo == null)
                await firstMiningInfoSetCompletionSource.Task.ConfigureAwait(false);
            return CurrentInfo;
        }
        TaskCompletionSource<object> firstMiningInfoSetCompletionSource = new TaskCompletionSource<object>();
        private int Compare(MiningInfo left, MiningInfo right)
        {
            ulong.TryParse(left.Height, out var leftHeight);
            ulong.TryParse(right.Height, out var rightHeight);
            if (leftHeight > rightHeight) return -1;
            if (rightHeight > leftHeight) return 1;
            return 0;
        }
        private StratumSettings GetSettings()
        {
            var ret = new StratumSettings();
            _configuration.Bind("Stratum", ret);
            return ret;
        }
        protected override async Task ExecuteAsync(CancellationToken stopCancellationToken)
        {
            _logger.LogInformation("Starting wallet poller");
            while (!stopCancellationToken.IsCancellationRequested)
            {


                try
                {
                    var currentOptions = GetSettings();
                    for (var x = 0; x < _workQueue.Count; x++)
                    {
                        var item = _workQueue[x];
                        if (item.Item2.IsCompleted)
                        {
                            _workQueue.RemoveAt(x);
                            x--;
                            try
                            {
                                var response = await item.Item2.ConfigureAwait(false);
                                if (response.IsSuccessStatusCode)
                                {
                                    var json = await response.Content.ReadAsStringAsync();
                                    var newInfo = JObject.Parse(json).ToObject<MiningInfo>();
                                    if (newInfo == null) continue;
                                    if (CurrentInfo == null || Compare(CurrentInfo, newInfo) > 0)
                                    {
                                        _logger.LogInformation($"New block {newInfo.Height} from: {item.Item1.ToLowerInvariant()}");
                                        newInfo.TargetDeadline = currentOptions.MaximumDeadline;
                                        CurrentInfo = newInfo;
                                    }
                                }
                            }
                            catch
                            {
                                // Ignored.
                            }
                        }
                    }
                    foreach (var url in currentOptions.Wallets)
                    {
                        if (_workQueue.Any(i => i.Item1 == url.ToUpperInvariant()))
                        {
                            continue;
                        }
                        try
                        {
                            _workQueue.Add(Tuple.Create(url.ToUpperInvariant(), Task.Factory.StartNew(async () => await CreateJobForUrl(url, currentOptions.PollIntervalSeconds, stopCancellationToken)).Unwrap()));
                        }
                        catch
                        {

                        }
                    }
                    await Task.Delay(TimeSpan.FromMilliseconds(25), stopCancellationToken);
                }
                catch
                {

                }
            }
        }
        readonly HttpClient _client;
        private async Task<HttpResponseMessage> CreateJobForUrl(string url, double pollIntervalSeconds, CancellationToken stopCancellationToken)
        {
            await Task.Yield();
            var baseUrl = new Uri(url);
            var target = new Uri(baseUrl, "/burst?requestType=getMiningInfo");
            await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds), stopCancellationToken).ConfigureAwait(false);
            try
            {
                return await _client.GetAsync(target, stopCancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw;
            }
        }
    }
}