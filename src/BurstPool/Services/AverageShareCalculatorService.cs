using System.Threading;
using System.Threading.Tasks;
using BurstPool.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StrangeSoft.Burst;

namespace BurstPool.Services
{
    public class AverageShareCalculatorService : BackgroundJob
    {
        private ILogger Logger { get; }
        public AverageShareCalculatorService(IBlockHeightTracker blockHeightTracker, IConfiguration configuration, ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory)
        {
            Logger = loggerFactory.CreateLogger("Averager");
        }
        protected override async Task ExecuteAsync(CancellationToken stopCancellationToken)
        {

        }
    }
}