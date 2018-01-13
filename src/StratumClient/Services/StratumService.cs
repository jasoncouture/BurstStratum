using System.Threading;
using System.Threading.Tasks;
using StrangeSoft.Burst;
using StratumClient.Services.Interfaces;
using System.Net.WebSockets;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace StratumClient.Services
{
    public class StratumService : BackgroundJob, IStratum
    {
        private readonly Uri _uri;
        private ILogger _logger;
        private ClientWebSocket _webSocket = null;
        public event EventHandler MiningInfoUpdated;
        private MiningInfo _miningInfo = null;
        private void OnMiningInfoUpdated()
        {
            MiningInfoUpdated?.Invoke(this, EventArgs.Empty);
        }
        public async Task<MiningInfo> GetMiningInfoAsync(CancellationToken cancellationToken)
        {
            if (MiningInfo != null)
                await Task.Factory.StartNew(() => _firstMiningInfoCompletionSource.Task.Wait(cancellationToken), cancellationToken);
            return MiningInfo;
        }
        public MiningInfo MiningInfo
        {
            get => _miningInfo;
            set
            {
                if (value == null) return;
                lock (_lockObject)
                {
                    if (_miningInfo != null && _miningInfo.GenerationSignature == value.GenerationSignature && _miningInfo.Height == value.Height)
                    {
                        return;
                    }
                    _miningInfo = value;
                    _firstMiningInfoCompletionSource.TrySetResult(null);
                }
                OnMiningInfoUpdated();
                _logger.LogInformation("New block detected, height: {Height}, Base Target: {BaseTarget}, GenSig: {GenerationSignature}", value.Height, value.BaseTarget, value.GenerationSignature);
            }
        }
        private object _lockObject = new object();
        private readonly TaskCompletionSource<object> _firstMiningInfoCompletionSource = new TaskCompletionSource<object>();
        public StratumService(IConfiguration configuration, ILogger<StratumService> logger)
        {
            _logger = logger;
            if (!Uri.TryCreate(configuration.GetValue<string>("StratumUri"), UriKind.Absolute, out var uri))
            {
                InvalidUri();
            }
            _uri = uri;
            switch (_uri.Scheme.ToLower())
            {
                case "ws":
                case "wss":
                    break;
                default:
                    InvalidUri();
                    // Unreachable.
                    return;
            }
        }

        private void InvalidUri()
        {
            _logger.LogCritical("FATAL: Unable to parse StratumUri, this URI must be a valid websocket URL, using the ws or wss scheme. The default value is wss://logg.coffee/stratum please edit your appsettings.json file with a valid value for this setting.");
            throw new InvalidOperationException("Invalid stratum URI");
        }

        protected override async Task ExecuteAsync(CancellationToken stopCancellationToken)
        {
            while (true)
            {
                try
                {
                    var buffer = new ArraySegment<byte>(new byte[16384]);
                    _webSocket = new ClientWebSocket();
                    _logger.LogInformation("Connecting to {URI}", _uri);
                    await _webSocket.ConnectAsync(_uri, stopCancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Connected.");
                    while (_webSocket.State == WebSocketState.Open)
                    {
                        var result = await _webSocket.ReceiveAsync(buffer, stopCancellationToken).ConfigureAwait(false);
                        if (result.Count > 0)
                        {
                            var jsonString = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                            JObject jobj;
                            try
                            {
                                jobj = JObject.Parse(jsonString);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to parse JSON data from stratum server.");
                                await Task.Delay(250).ConfigureAwait(false);
                                continue;
                            }
                            if (!jobj.TryGetValue("type", StringComparison.OrdinalIgnoreCase, out var value) || value.Type != JTokenType.String) continue;
                            switch (value.ToObject<string>().ToLowerInvariant())
                            {
                                case "heartbeat":
                                    _logger.LogInformation("Got a heartbeat message from server.");
                                    break;
                                case "mininginfo":
                                    var miningData = jobj.GetValue("data", StringComparison.OrdinalIgnoreCase) as JObject;
                                    var miningInfo = miningData?.ToObject<MiningInfo>();
                                    MiningInfo = miningInfo;
                                    break;
                                default:
                                    _logger.LogWarning("Unknown JSON message type: {MessageType}, full message: {Message}", value.ToObject<string>(), jobj.ToString(Formatting.Indented));
                                    break;

                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (stopCancellationToken.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Websocket thread error, will wait 5s and try again.");
                    await Task.Delay(5000).ConfigureAwait(false);
                }
            }
        }
    }
}