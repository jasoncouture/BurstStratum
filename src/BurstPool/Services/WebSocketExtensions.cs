using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BurstPool.Services
{
    public static class WebSocketExtensions
    {
        public static async Task<T> ReadObjectAsync<T>(this WebSocket socket, CancellationToken cancellationToken = default(CancellationToken)) where T : class
        {
            var buffer = new ArraySegment<byte>(new byte[16384]);
            var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if(result.MessageType == WebSocketMessageType.Close) {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, 
                      String.Empty, cancellationToken).ConfigureAwait(false); 
                return null;
            }
            var jsonText = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
            return JToken.Parse(jsonText).ToObject<T>();
        }
        public static async Task SendObjectAsync<T>(this WebSocket socket, T obj, CancellationToken cancellationToken = default(CancellationToken))
        {
            var segment = JObject.FromObject(obj).ToString(Formatting.None);
            await socket.SendTextAsync(segment, cancellationToken).ConfigureAwait(false);
        }

        public static async Task SendTextAsync(this WebSocket socket, string data, CancellationToken cancellationToken = default(CancellationToken))
        {
            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(data)), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
    }
}