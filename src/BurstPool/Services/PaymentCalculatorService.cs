using System;
using System.Threading;
using System.Threading.Tasks;
using BurstPool.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StrangeSoft.Burst;

namespace BurstPool.Services
{
    public class PaymentCalculatorService : BackgroundJob
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger _logger;
        public PaymentCalculatorService(IConfiguration configuration, IServiceScopeFactory serviceScopeFactory, ILoggerFactory loggerFactory, IBlockHeightTracker blockHeightTracker)
        {
            _configuration = configuration;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = loggerFactory.CreateLogger("PaymentCalculator");
        }
        protected override async Task ExecuteAsync(CancellationToken stopCancellationToken)
        {
            while (!stopCancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stopCancellationToken);
                    var enabled = _configuration.GetSection("Pool").GetValue<bool>("EnablePayoutCalculation", false);
                    if(!enabled) {
                        _logger.LogInformation("Payouts are disabled. (Pool.EnablePayoutCalculation is not set to true)");
                        continue;
                    }
                    var poolBurstAccount = _configuration.GetSection("Pool").GetValue<string>("Account");
                    if(string.IsNullOrWhiteSpace(poolBurstAccount)) {
                        _logger.LogError("Pool payment calculations are enabled, but the pool account is not set. Please set Pool.PoolAccount to either the numeric account, or the BURST address for the pool.");
                        continue;
                    }
                }
                catch (OperationCanceledException) when (stopCancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    
                }
            }
        }
    }
}