using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrangeSoft.Burst;
using StratumClient.Services;
using StratumClient.Services.Interfaces;

namespace StratumClient
{
    public class Startup
    {
        public Startup(IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            Configuration = configuration;
            Logger = loggerFactory.CreateLogger("Startup");
        }
        public ILogger Logger { get; }
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            Logger.LogInformation($"Stratum Client Proxy - {DateTime.Now.Year} Jason Couture");
            Logger.LogInformation($"Donations appreciated at: BURST-49LB-USA3-LWZJ-9PCJP");
            services.AddMvc();
            services.AddLogging();
            services.AddBackgroundJobSingleton<IStratum, StratumService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();
        }
    }
}
