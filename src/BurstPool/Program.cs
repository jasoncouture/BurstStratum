using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using BurstPool.Database;
using Microsoft.EntityFrameworkCore;

namespace BurstPool
{
    public static class Program
    {
        public static IWebHost MigrateDatabase(this IWebHost webHost)
        {
            using (var serviceScope = webHost.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {

                serviceScope.ServiceProvider.GetRequiredService<PoolContext>().Database.Migrate();
            }
            return webHost;
        }
        public static void Main(string[] args)
        {
            BuildWebHost(args).MigrateDatabase().Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .Build();
    }
}
