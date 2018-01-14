using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace BurstPool.Services
{
    public static class HttpContentExtensions
    {
        public static async Task<T> ReadAsObjectAsync<T>(this HttpContent httpContent)
        {
            return JToken.Parse(await httpContent.ReadAsStringAsync().ConfigureAwait(false)).ToObject<T>();
        }
    }
}