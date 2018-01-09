using System;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BurstStratum.Services;
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
                    
                    if (context.Request.Query.TryGetValue("protocol", out var protocol) && protocol.Count > 0)
                    {
                        if (string.Equals(protocol.FirstOrDefault(), "bin", StringComparison.OrdinalIgnoreCase))
                        {
                            var socket = await context.WebSockets.AcceptWebSocketAsync();
                            await RunBinaryStratum(context, socket);
                        }
                        else if (!string.Equals(protocol.FirstOrDefault(), "json", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Response.StatusCode = 400;
                            return;
                        }
                    } 
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await RunStratum(context, webSocket);
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
        private async Task SendStratumAsync(WebSocket socket, SemaphoreSlim semaphore, IStratumMessage message)
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                await socket.SendAsync(new ArraySegment<byte>(message.Build()), WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            finally
            {
                semaphore.Release();
            }
        }
        private async Task SendObjectAsync<T>(WebSocket socket, T payload)
        {
            var bytes = Encoding.UTF8.GetBytes(JObject.FromObject(payload).ToString(Formatting.None));
            await socket.SendAsync(new ArraySegment<byte>(bytes, 0, bytes.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        private async Task RunBinaryStratum(HttpContext context, WebSocket socket)
        {
            SemaphoreSlim socketSemaphore = new SemaphoreSlim(1, 1);
            await SendStratumAsync(socket, socketSemaphore, new ServerGreetingStratumMessage()).ConfigureAwait(false);
            long lastSend = DateTimeOffset.Now.ToUnixTimeSeconds();
            EventHandler eventHandler = async (s, e) =>
            {
                if (socket.State != WebSocketState.Open)
                {
                    return;
                }
                await SendStratumAsync(socket, socketSemaphore, new MiningInfoStratumMessage(await _poller.GetCurrentMiningInfoAsync().ConfigureAwait(false))).ConfigureAwait(false);
            };
            _poller.MiningInfoChanged += eventHandler;
            try
            {
                await SendStratumAsync(socket, socketSemaphore, new MiningInfoStratumMessage(await _poller.GetCurrentMiningInfoAsync().ConfigureAwait(false))).ConfigureAwait(false);
                while (socket.State == WebSocketState.Open)
                {
                    await Task.Delay(10000);
                    if (DateTimeOffset.Now.ToUnixTimeSeconds() - lastSend > 30)
                    {
                        await SendStratumAsync(socket, socketSemaphore, new HeartbeatStratumMessage());
                    }
                }
            }
            finally
            {
                _poller.MiningInfoChanged -= eventHandler;
            }
        }

        private async Task RunStratum(HttpContext context, WebSocket socket)
        {
            var startupMiningInfo = await _poller.GetCurrentMiningInfoAsync().ConfigureAwait(false);
            await SendObjectAsync(socket, new
            {
                type = "miningInfo",
                data = startupMiningInfo
            });
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
                    await Task.Delay(10000);
                    await socketSemaphore.WaitAsync();
                    try
                    {
                        if (DateTimeOffset.Now.ToUnixTimeSeconds() - lastSend > 30)
                        {
                            await SendObjectAsync(socket, new
                            {
                                type = "heartbeat",
                                data = new { timestamp = DateTimeOffset.Now.ToUnixTimeSeconds() }
                            });
                            lastSend = DateTimeOffset.Now.ToUnixTimeSeconds();
                        }
                    }
                    finally
                    {
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