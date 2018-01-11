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
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBackgroundJobSingleton<TInterface, TImplementation>(this IServiceCollection services) where TImplementation : class, TInterface, IHostedService
        where TInterface : class
        {
            return services.AddBackgroundJobSingleton<TImplementation>()
                .AddSingleton<TInterface>(provider => provider.GetRequiredService<TImplementation>());
        }

        public static IServiceCollection AddBackgroundJobSingleton<TImplementation>(this IServiceCollection services) where TImplementation : class, IHostedService
        {
            return services.AddSingleton<TImplementation, TImplementation>()
                .AddSingleton<IHostedService>(provider => provider.GetRequiredService<TImplementation>());
        }
    }
}