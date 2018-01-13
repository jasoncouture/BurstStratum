using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using BurstPool.Database;
using BurstPool.Services;
using BurstPool.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BurstPool.Controllers
{
    [Route("api/events")]
    public class EventsController : Controller
    {
        private IServiceScopeFactory _scopeFactory;
        private IBlockHeightTracker _blockHeightTracker;
        public EventsController(IServiceScopeFactory scopeFactory, IBlockHeightTracker blockHeightTracker)
        {
            _scopeFactory = scopeFactory;
            _blockHeightTracker = blockHeightTracker;
        }

        public class WebSocketResult : IActionResult
        {
            private readonly Func<HttpContext, WebSocket, Task> _callback;
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
                await _callback(context.HttpContext, socket);
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Request Finished", context.HttpContext.RequestAborted);
            }
        }
        [HttpGet("subscribe/{id}")]
        public async Task<IActionResult> Subscribe(ulong id)
        {
            return new WebSocketResult(async (httpContext, socket) =>
            {
                decimal lastAverage = -1;
                decimal lastBest = -1;
                decimal lastWorst = -1;
                long lastHeight = -1;
                while (socket.State == WebSocketState.Open)
                {
                    await Task.Delay(5000, httpContext.RequestAborted);
                    int counter = 0;
                    object toSend = null;
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<PoolContext>();
                        var currentBlock = await _blockHeightTracker.GetCurrentBlockHeightAsync(httpContext.RequestAborted).ConfigureAwait(false);
                        if (!long.TryParse(currentBlock.Height, out var height)) continue;
                        height = height - 360; // Rewind 360 blocks.
                        var rawData = await context.Shares.Where(i => i.AccountId == id && i.BlockId >= height).GroupBy(i => i.BlockId).Select(x => x.OrderByDescending(i => i.ShareValue).First()).ToListAsync(httpContext.RequestAborted).ConfigureAwait(false);
                        var data = rawData.Select(i => i.ShareValue).ToList();
                        var averageShares = data.DefaultIfEmpty(0m).Sum() / 360m;;
                        var bestShare = data.DefaultIfEmpty(0m).Max();
                        var worstShare = data.DefaultIfEmpty(0m).Min();
                        var bestDeadline = rawData.OrderBy(i => i.Deadline).FirstOrDefault();
                        var worstDeadline = rawData.OrderByDescending(i => i.Deadline).FirstOrDefault();
                        var recentDeadline = rawData.OrderBy(i => i.Created).FirstOrDefault();
                        var bestShareData = rawData.OrderByDescending(i => i.ShareValue).FirstOrDefault();
                        var worstShareData = rawData.OrderBy(i => i.ShareValue).FirstOrDefault();
                        var recentHeight = recentDeadline?.BlockId ?? -1;
                        if (lastHeight == recentHeight && averageShares == lastAverage && lastBest == bestShare && lastWorst == worstShare && counter < 30)
                        {
                            counter += 1;
                            continue;
                        }
                        counter = 0;
                        lastAverage = averageShares;
                        lastBest = bestShare;
                        lastWorst = worstShare;
                        lastHeight = recentHeight;
                        toSend = new
                        {
                            accountId = id,
                            averageShares,
                            best = new
                            {
                                deadline = new
                                {
                                    height = bestDeadline?.BlockId,
                                    nonce = bestDeadline?.Nonce,
                                    deadline = bestDeadline?.Deadline,
                                    shares = bestDeadline?.ShareValue
                                },
                                share = new
                                {
                                    height = bestShareData?.BlockId,
                                    nonce = bestShareData?.Nonce,
                                    deadline = bestShareData?.Deadline,
                                    shares = bestShareData?.ShareValue
                                }
                            },
                            worst = new
                            {
                                deadline = new
                                {
                                    height = worstDeadline?.BlockId,
                                    nonce = worstDeadline?.Nonce,
                                    deadline = worstDeadline?.Deadline,
                                    shares = worstDeadline?.ShareValue
                                },
                                share = new
                                {
                                    height = worstShareData?.BlockId,
                                    nonce = worstShareData?.Nonce,
                                    deadline = worstShareData?.Deadline,
                                    shares = worstShareData?.ShareValue
                                }
                            },
                            last = new
                            {
                                height = recentDeadline?.BlockId,
                                nonce = recentDeadline?.Nonce,
                                deadline = recentDeadline?.Deadline,
                                shares = recentDeadline?.ShareValue
                            }
                        };
                    }
                    if (toSend != null)
                        await socket.SendObjectAsync(toSend, httpContext.RequestAborted).ConfigureAwait(false);
                }
            });
        }
    }
}