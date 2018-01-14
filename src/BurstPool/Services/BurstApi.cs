using System;
using System.Net.Http;
using System.Threading.Tasks;
using BurstPool.Models;
using BurstPool.Services.Interfaces;
using Microsoft.AspNetCore.Http;

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

        Uri BurstRequestUri(string requestType, QueryString parameters = default(QueryString))
        {
            parameters = parameters.Add("requestType", requestType);
            return _uriFactory.GetUri(requestType, "/burst", parameters);
        }
        public async Task<AccountAddress> GetAccountAddressAsync(string address)
        {
            QueryString qs = QueryString.Empty.Add("account", address);
            var response = await _httpClient.GetAsync(BurstRequestUri("rsConvert", qs)).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsObjectAsync<AccountAddress>();
        }

        public Task<AccountAddress> GetAccountAddressAsync(ulong accountId)
        {
            return GetAccountAddressAsync(accountId.ToString());
        }
    }
}