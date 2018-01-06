using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BurstStratum.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BurstStratum.Controllers
{
    public class StratumWebsocketMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private readonly IMiningInfoPoller _poller;
        public StratumWebsocketMiddleware(RequestDelegate next, ILoggerFactory loggerFactory, IMiningInfoPoller miningInfoPoller)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<StratumWebsocketMiddleware>();
            _poller = miningInfoPoller;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Path == "/stratum")
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var socket = await context.WebSockets.AcceptWebSocketAsync();
                    await RunStratum(context, socket);
                }
                else
                {
                    context.Response.StatusCode = 400;
                }
            }
            else
            {
                await _next(context);
            }
        }
        private async Task SendObjectAsync<T>(WebSocket socket, T payload)
        {
            var bytes = Encoding.UTF8.GetBytes(JObject.FromObject(payload).ToString(Formatting.None));
            await socket.SendAsync(new ArraySegment<byte>(bytes, 0, bytes.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        private async Task RunStratum(HttpContext context, WebSocket socket)
        {
            var startupMiningInfo = await _poller.GetCurrentMiningInfoAsync().ConfigureAwait(false);
            SemaphoreSlim socketSemaphore = new SemaphoreSlim(1, 1);
            long lastSend = DateTimeOffset.Now.ToUnixTimeSeconds();
            EventHandler eventHandler = async (s, e) =>
            {
                if (socket.State != WebSocketState.Open)
                {
                    return;
                }
                await socketSemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    var currentMiningInfo = await _poller.GetCurrentMiningInfoAsync().ConfigureAwait(false);
                    await SendObjectAsync(socket, new
                    {
                        type = "miningInfo",
                        data = currentMiningInfo
                    });
                    lastSend = DateTimeOffset.Now.ToUnixTimeSeconds();
                }
                finally
                {
                    socketSemaphore.Release();
                }
            };
            _poller.MiningInfoChanged += eventHandler;
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    await Task.Delay(30000);
                    await socketSemaphore.WaitAsync();
                    try {
                        if(DateTimeOffset.Now.ToUnixTimeSeconds() - lastSend > 30) {
                            await SendObjectAsync(socket, new { 
                                type = "heartbeat",
                                data = new { timestamp = DateTimeOffset.Now.ToUnixTimeSeconds() }
                            });
                            lastSend = DateTimeOffset.Now.ToUnixTimeSeconds();
                        }
                    } finally {
                        socketSemaphore.Release();
                    }
                }

            }
            catch
            {

            }
            finally
            {
                _poller.MiningInfoChanged -= eventHandler;
            }
        }
    }
}