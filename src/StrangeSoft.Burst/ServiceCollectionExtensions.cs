using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace StrangeSoft.Burst
{
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