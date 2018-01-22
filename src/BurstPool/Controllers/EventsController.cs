using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using BurstPool.Database;
using BurstPool.Services;
using BurstPool.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;

namespace BurstPool.Controllers
{
    [Route("api/events")]
    public class EventsController : Controller
    {
        private IServiceScopeFactory _scopeFactory;
        private IBlockHeightTracker _blockHeightTracker;
        private IMessenger _messenger;
        public EventsController(IServiceScopeFactory scopeFactory, IBlockHeightTracker blockHeightTracker, IMessenger messenger)
        {
            _scopeFactory = scopeFactory;
            _blockHeightTracker = blockHeightTracker;
            _messenger = messenger;
        }
        public class SubscriptionResult : WebSocketResult
        {
            private readonly IMessenger _messenger;

            private sealed class JoinedDisposable : IDisposable
            {
                Dictionary<string, IDisposable> _disposables = new Dictionary<string, IDisposable>(StringComparer.OrdinalIgnoreCase);
                public JoinedDisposable()
                {
                }
                public void Remove(string key)
                {
                    if (key == null) throw new ArgumentNullException(nameof(key));
                    lock (_disposables)
                    {
                        if (_disposables.ContainsKey(key))
                        {
                            try
                            {
                                _disposables[key]?.Dispose();
                            }
                            catch
                            {
                                // Ignored.
                            }
                            _disposables.Remove(key);
                        }
                    }
                }
                public void Add(string key, IDisposable disposable)
                {
                    if (key == null) throw new ArgumentNullException(nameof(key));
                    if (disposable == null) throw new ArgumentNullException(nameof(disposable));
                    lock (_disposables)
                    {
                        if (_disposed) throw new ObjectDisposedException(nameof(JoinedDisposable));
                        if (_disposables.ContainsKey(key))
                        {
                            try
                            {
                                _disposables[key]?.Dispose();
                            }
                            catch
                            {
                                // Ignored.
                            }
                        }
                        _disposables[key] = disposable;
                    }
                }
                private bool _disposed = false;
                public void Dispose()
                {
                    lock (_disposables)
                    {
                        if (_disposed) return;
                        _disposed = true;
                        foreach (var item in _disposables)
                        {
                            try
                            {
                                item.Value?.Dispose();
                            }
                            catch
                            {
                                // Ignored.
                            }
                        }
                    }
                }
            }
            public SubscriptionResult(IMessenger messenger)
            {
                _messenger = messenger;
            }

            protected override async Task ExecuteAsync(HttpContext context, WebSocket socket)
            {
                await Task.Yield();
                SemaphoreSlim socketSemaphore = new SemaphoreSlim(1, 1);
                async void handler(object sender, object e)
                {
                    await socketSemaphore.WaitAsync(context.RequestAborted);
                    try
                    {
                        if (socket.State != WebSocketState.Open) return;
                        await socket.SendObjectAsync(e, context.RequestAborted).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ignored.
                    }
                    finally
                    {
                        socketSemaphore.Release();
                    }
                }
                var subscription = Guid.NewGuid().ToString("n");

                using (var subscriptions = new JoinedDisposable())
                {
                    subscriptions.Add(string.Empty, _messenger.Subscribe($"WebSocket.{subscription}", handler));
                    await socket.SendObjectAsync(new {
                        @event = "Connected",
                        data = null as object
                    });
                    while (socket.State == WebSocketState.Open)
                    {
                        var result = await socket.ReadObjectAsync<JObject>(context.RequestAborted);
                        if (result == null) continue;
                        if (result.TryGetValue("subscribe", StringComparison.OrdinalIgnoreCase, out var subscribeTo))
                        {
                            HandleSubscribes(subscribeTo, subscription, subscriptions);
                        }
                        if (result.TryGetValue("unsubscribe", StringComparison.OrdinalIgnoreCase, out var unsubscribeFrom))
                        {
                            HandleUnsubscribes(unsubscribeFrom, subscription, subscriptions);
                        }

                    }
                }
            }


            private void HandleUnsubscribes(JToken unsubscribeFrom, string subscription, JoinedDisposable subscriptions)
            {
                if (unsubscribeFrom.Type == JTokenType.String)
                {
                    HandleUnsubscribe(unsubscribeFrom.ToObject<string>(), subscription, subscriptions);
                }
                else if (unsubscribeFrom.Type == JTokenType.Array)
                {
                    var jarray = (JArray)unsubscribeFrom;
                    foreach (var item in jarray.Children().OfType<JValue>().Where(i => i.Type == JTokenType.String).Select(x => x.ToObject<string>()))
                    {
                        HandleUnsubscribe(item, subscription, subscriptions);
                    }
                }
            }

            void HandleUnsubscribe(string unsubscribeFrom, string subscription, JoinedDisposable subscriptions)
            {
                subscriptions.Remove(unsubscribeFrom);
            }

            private void HandleSubscribes(JToken subscribeTo, string subscription, JoinedDisposable subscriptions)
            {
                if (subscribeTo.Type == JTokenType.String)
                {
                    HandleSubscribe(subscribeTo.ToObject<string>(), subscription, subscriptions);
                }
                else if (subscribeTo.Type == JTokenType.Array)
                {
                    var jarray = (JArray)subscribeTo;
                    foreach (var item in jarray.Children().OfType<JValue>().Where(i => i.Type == JTokenType.String).Select(x => x.ToObject<string>()))
                    {
                        HandleSubscribe(item, subscription, subscriptions);
                    }
                }
            }

            private void HandleSubscribe(string item, string subscription, JoinedDisposable subscriptions)
            {
                void handler(object sender, object data) => _messenger.Publish($"WebSocket.{subscription}", sender, new
                {
                    @event = item,
                    data
                });
                subscriptions.Add(item, _messenger.Subscribe($"Public.{item}", handler));
            }
        }
        public class WebSocketResult : IActionResult
        {
            private readonly Func<HttpContext, WebSocket, Task> _callback;
            protected WebSocketResult()
            {

            }
            protected virtual Task ExecuteAsync(HttpContext context, WebSocket socket)
            {
                return _callback.Invoke(context, socket);
            }
            public WebSocketResult(Func<HttpContext, WebSocket, Task> callback)
            {
                _callback = callback;
            }
            public async Task ExecuteResultAsync(ActionContext context)
            {
                if (!context.HttpContext.WebSockets.IsWebSocketRequest)
                {
                    context.HttpContext.Response.StatusCode = 400;
                    return;
                }
                var socket = await context.HttpContext.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
                await ExecuteAsync(context.HttpContext, socket);
                if (socket.State == WebSocketState.Open)
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Request Finished", context.HttpContext.RequestAborted);
            }
        }
        [HttpGet("")]
        public async Task<IActionResult> Subscribe()
        {
            return new SubscriptionResult(_messenger);
        }
    }
}