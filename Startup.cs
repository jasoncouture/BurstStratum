using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BurstStratum.Controllers;
using BurstStratum.Services;
using BurstStratum.Services.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BurstStratum
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddSingleton<MiningInfoPoller>()
            .AddSingleton<IHostedService>(resolver => resolver.GetRequiredService<MiningInfoPoller>())
            .AddSingleton<IMiningInfoPoller>(resolver => resolver.GetRequiredService<MiningInfoPoller>());
            
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseMiddleware<BurstMiningMiddleware>();
            app.UseMvc();
        }
    }
}
