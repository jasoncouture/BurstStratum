using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using BurstStratum.Configuration;
using BurstStratum.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BurstStratum.Controllers
{
    public class BurstMiningMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<BurstMiningMiddleware> _logger;
        private readonly IMiningInfoPoller _poller;
        public BurstMiningMiddleware(RequestDelegate next, ILoggerFactory loggerFactory, IMiningInfoPoller miningInfoPoller)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<BurstMiningMiddleware>();
            _poller = miningInfoPoller;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Path == "/burst" && context.Request.Query["requestType"] == "getMiningInfo")
            {
                var miningInfo = await _poller.GetCurrentMiningInfoAsync();
                var data = Encoding.UTF8.GetBytes(JObject.FromObject(miningInfo).ToString(Formatting.None));
                context.Response.ContentType = "application/json";
                await context.Response.Body.WriteAsync(data, 0, data.Length);
            }
            else
            {
                await _next(context);
            }
        }
    }
}
