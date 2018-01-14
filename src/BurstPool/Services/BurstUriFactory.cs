using System;
using System.Linq;
using BurstPool.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BurstPool.Services
{
    public class BurstUriFactory : IBurstUriFactory
    {
        private IConfiguration _configuration;
        public BurstUriFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Uri GetUri(string requestType, PathString pathString = default(PathString), QueryString queryString = default(QueryString))
        {
            var targets = _configuration.GetSection("Pool").GetSection("Wallets").GetSection(requestType).AsEnumerable().Select(i => i.Value).Where(i => !string.IsNullOrWhiteSpace(i)).ToArray();
            if (targets.Length == 0)
                targets = _configuration.GetSection("Pool").GetSection("Wallets").GetSection("Default").AsEnumerable().Select(i => i.Value).Where(i => !string.IsNullOrWhiteSpace(i)).ToArray().ToArray();
            if (targets.Length == 0)
                targets = new string[] { _configuration.GetSection("Pool").GetValue<string>("TrustedWallet") };
            var target = targets.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).OrderBy(i => Guid.NewGuid()).FirstOrDefault();
            var targetUri = new Uri(target);
            var builder = new UriBuilder();
            builder.Scheme = targetUri.Scheme;
            builder.Host = targetUri.DnsSafeHost;
            if (!targetUri.IsDefaultPort)
                builder.Port = targetUri.Port;
            if (!string.IsNullOrWhiteSpace(targetUri.AbsolutePath) && targetUri.AbsolutePath != "/")
                builder.Path = new PathString(targetUri.AbsolutePath).Add(pathString);
            else
                builder.Path = pathString;
            if (string.IsNullOrWhiteSpace(builder.Path)) builder.Path = "/";
            builder.Query = queryString.ToUriComponent();
            return builder.Uri;
        }
    }
}