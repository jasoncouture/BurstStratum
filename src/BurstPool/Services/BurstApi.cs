using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BurstPool.Models;
using BurstPool.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;

namespace BurstPool.Services
{
    public class BurstApi : IBurstApi
    {
        private readonly IBurstUriFactory _uriFactory;

        public BurstApi(IBurstUriFactory uriFactory)
        {
            _uriFactory = uriFactory;
        }
        HttpClient _httpClient = new HttpClient();
        QueryString FromDictionary(Dictionary<string, object> dictionary)
        {
            QueryString ret = QueryString.Empty;
            foreach (var kvp in dictionary)
            {
                var value = $"{kvp.Value}";
                if (string.IsNullOrEmpty(value)) continue;
                ret = ret.Add(kvp.Key, value);
            }
            return ret;
        }
        Uri BurstRequestUri(string requestType, QueryString parameters = default(QueryString))
        {
            parameters = parameters.Add("requestType", requestType);
            return _uriFactory.GetUri(requestType, "/burst", parameters);
        }
        public async Task<AccountAddress> GetAccountAddressAsync(string address, CancellationToken cancellationToken = default(CancellationToken))
        {
            var qs = FromDictionary(new Dictionary<string, object>() {
                { "account", address }
            });
            var response = await _httpClient.GetAsync(BurstRequestUri("rsConvert", qs), cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsObjectAsync<AccountAddress>();
        }

        public Task<AccountAddress> GetAccountAddressAsync(ulong accountId, CancellationToken cancellationToken = default(CancellationToken))
        {
            return GetAccountAddressAsync(accountId.ToString(), cancellationToken);
        }

        public async Task<string> GetRewardReceipientAsync(string account, CancellationToken cancellationToken = default(CancellationToken))
        {
            var query = FromDictionary(new Dictionary<string, object>() {
                { nameof(account), account }
            });
            var target = BurstRequestUri("getRewardRecipient", query);
            var response = await _httpClient.GetAsync(target, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsObjectAsync<JObject>();
            
            var jvalue = result.GetValue("rewardRecipient", StringComparison.OrdinalIgnoreCase);
            if(jvalue.Type != JTokenType.String) throw new InvalidOperationException("Unexpected json token type");
            return jvalue.ToObject<string>();
        }
        public async Task<BlockDetails> GetBlockAsync(long height, CancellationToken cancellationToken = default(CancellationToken))
        {
            var query = FromDictionary(new Dictionary<string, object>() {
                { nameof(height), height }
            });
            var target = BurstRequestUri("getBlock", query);
            var response = await _httpClient.GetAsync(target, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsObjectAsync<BlockDetails>();
        }
    }
}