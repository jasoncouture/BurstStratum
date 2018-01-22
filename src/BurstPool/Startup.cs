using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BurstPool.Database;
using BurstPool.Services;
using BurstPool.Services.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SpaServices.Webpack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StrangeSoft.Burst;

namespace BurstPool
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
            services.AddDbContextPool<PoolContext>(options => options.UseMySql(Configuration.GetConnectionString("Default")));
            services.AddBackgroundJobSingleton<IBlockHeightTracker, BlockHeightTracker>();
            services.AddBackgroundJobSingleton<AverageShareCalculatorService>();
            services.AddBackgroundJobSingleton<BlockStateTracker>();
            services.AddSingleton<IMessenger>(provider => Messenger.Instance);
            services.AddSingleton<IShareCalculator, ShareCalculator>();
            services.AddSingleton<IBurstUriFactory, BurstUriFactory>();
            services.AddSingleton<IBurstApi, BurstApi>();
            services.AddScoped<IShareTracker, ShareTracker>();
            //services.AddBackgroundJobSingleton<PayoutService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseWebpackDevMiddleware(new WebpackDevMiddlewareOptions
                {
                    HotModuleReplacement = true
                });
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();
            app.UseWebSockets();
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");

                routes.MapSpaFallbackRoute(
                    name: "spa-fallback",
                    defaults: new { controller = "Home", action = "Index" });
            });
        }
    }
}
