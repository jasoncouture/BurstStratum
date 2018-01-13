using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BurstPool.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BurstPool.Controllers
{
    [Route("burst")]
    public class BurstController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IShareCalculator _shareCalculator;
        private readonly ILogger<BurstController> _logger;
        private readonly IBlockHeightTracker _blockHeightTracker;
        private readonly IShareTracker _shareTracker;
        public BurstController(IConfiguration configuration, IShareCalculator shareCalculator, ILoggerFactory loggerFactory, IBlockHeightTracker blockHeightTracker, IShareTracker shareTracker)
        {
            _configuration = configuration;
            _shareCalculator = shareCalculator;
            _logger = loggerFactory.CreateLogger<BurstController>();
            _blockHeightTracker = blockHeightTracker;
            _shareTracker = shareTracker;
        }
        private static HttpClient _httpClient = new HttpClient();
        private Uri GetProxyUri(string requestType)
        {
            return GetProxyUri(GetUrlOverride(requestType));
        }
        private Uri GetUrlOverride(string requestType)
        {
            var targets = _configuration.GetSection("Pool").GetSection("Wallets").GetSection(requestType).AsEnumerable().Select(i => i.Value).Where(i => !string.IsNullOrWhiteSpace(i)).ToArray();
            if (targets.Length == 0)
                targets = _configuration.GetSection("Pool").GetSection("Wallets").GetSection("Default").AsEnumerable().Select(i => i.Value).Where(i => !string.IsNullOrWhiteSpace(i)).ToArray().ToArray();
            if (targets.Length == 0)
                targets = new string[] { _configuration.GetSection("Pool").GetValue<string>("TrustedWallet") };
            var target = targets.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).OrderBy(i => Guid.NewGuid()).FirstOrDefault();
            return new Uri(target);
        }
        private Uri GetProxyUri()
        {
            return GetProxyUri(Request.QueryString);
        }
        private Uri GetProxyUri(Uri baseUri)
        {
            return GetProxyUri(baseUri, Request.QueryString);
        }
        private Uri GetProxyUri(QueryString queryString)
        {
            var wallet = _configuration.GetSection("Pool")?.GetValue<string>("TrustedWallet");
            return GetProxyUri(new Uri(wallet), queryString);
        }
        private Uri GetProxyUri(Uri target, QueryString queryString)
        {
            var builder = new UriBuilder();
            builder.Scheme = target.Scheme;
            builder.Host = target.DnsSafeHost;
            if (!target.IsDefaultPort)
                builder.Port = target.Port;
            if (!string.IsNullOrWhiteSpace(target.AbsolutePath) && target.AbsolutePath != "/")
                builder.Path = new PathString(target.AbsolutePath).Add(Request.Path);
            else
                builder.Path = Request.Path;
            builder.Query = queryString.ToUriComponent();
            _logger.LogInformation($"Forwarding request to {builder.Uri}");
            return builder.Uri;
        }
        private bool HasParameter(string parameter)
        {
            return GetQueryParameter(parameter) != null;
        }
        private string GetQueryParameter(string parameter)
        {
            if (!Request.Query.Keys.Any(i => string.Equals(parameter, i, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }
            var values = Request.Query.FirstOrDefault(i => string.Equals(i.Key, parameter, StringComparison.OrdinalIgnoreCase));
            return values.Value.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i));
        }
        [HttpPost]
        [HttpGet]
        public async Task<IActionResult> RespondAsync(string requestType)
        {
            Uri baseUri = null;
            switch (requestType)
            {
                case "getMiningInfo":
                    var stratum = _configuration.GetSection("Pool").GetValue<string>("Stratum");
                    if (stratum != null && Uri.TryCreate(stratum, UriKind.Absolute, out var stratumUri))
                    {
                        baseUri = stratumUri;
                    }
                    goto default;
                case "submitNonce":
                    if (!HttpMethods.IsPost(Request.Method))
                    {
                        ModelState.AddModelError("", "This request must be sent as a POST request.");
                    }
                    if (!ulong.TryParse(GetQueryParameter("blockHeight"), out var blockHeight))
                    {
                        ModelState.AddModelError("blockHeight", "Your miner must send blockHeight with the request to mine in this pool.");
                    }
                    if (!ulong.TryParse(GetQueryParameter("nonce"), out var nonce))
                    {
                        ModelState.AddModelError("nonce", "Nonce is invalid or missing, a valid nonce is required.");
                    }
                    ulong? accountId = null;
                    string secretPhrase = GetQueryParameter("secretPhrase");
                    if (ulong.TryParse(GetQueryParameter("accountId"), out var accountIdTemp))
                    {
                        accountId = accountIdTemp;
                    }
                    else if (HasParameter("accountId"))
                    {
                        ModelState.AddModelError("accountId", "Invalid account ID");
                    }

                    if (accountId == null && secretPhrase == null)
                    {
                        ModelState.AddModelError(nameof(accountId), "Account ID or secretPhrase is required.");
                        ModelState.AddModelError(nameof(secretPhrase), "Account ID or secretPhrase is required.");
                    }

                    if (!ModelState.IsValid)
                        return BadRequest(ModelState);
                    var block = await _blockHeightTracker.GetCurrentBlockHeightAsync();
                    if (blockHeight > 0)
                    {
                        bool firstWait = true;
                        while (true)
                        {
                            block = await _blockHeightTracker.GetCurrentBlockHeightAsync();
                            if (!ulong.TryParse(block.Height, out var newHeight))
                            {
                                await Task.Delay(250, HttpContext.RequestAborted).ConfigureAwait(false);
                                continue;
                            }
                            if (newHeight == blockHeight)
                            {
                                break;
                            }
                            else if (newHeight > blockHeight || (newHeight + _configuration.GetSection("Pool").GetValue<ulong>("MaximumPremineBlocks", 0L)) < blockHeight)
                            {
                                return Ok(new
                                {
                                    error = $"Your deadline is for a different block, your block: {blockHeight}, current block: {newHeight}"
                                });
                            }
                            else
                            {
                                await Task.Delay(100);
                            }
                            if (firstWait)
                            {
                                _logger.LogInformation($"Client {HttpContext.Connection.RemoteIpAddress} submitted a nonce for a future block, blocking client until block {blockHeight} appears on the network.");
                                firstWait = false;
                            }
                        }
                    }

                    var queryString = new QueryString();
                    queryString = queryString
                        .Add("requestType", "submitNonce")
                        .Add("nonce", nonce.ToString())
                        .Add("blockHeight", blockHeight.ToString());
                    if (accountId != null)
                        queryString = queryString.Add("accountId", accountId.Value.ToString());
                    var poolSecret = GetPoolSecretPhrase();
                    secretPhrase = secretPhrase ?? poolSecret;

                    if (!string.IsNullOrWhiteSpace(secretPhrase))
                        queryString = queryString.Add("secretPhrase", secretPhrase ?? poolSecret);
                    bool isPoolUser = string.IsNullOrWhiteSpace(secretPhrase) || (!string.IsNullOrWhiteSpace(poolSecret) && string.Equals(poolSecret, secretPhrase));
                    try
                    {
                        var response = await _httpClient.SendAsync(CreateProxyHttpRequest(HttpContext, GetProxyUri(queryString)), HttpContext.RequestAborted).ConfigureAwait(false);
                        var responseStringJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var responseObject = JObject.Parse(responseStringJson);
                        var newContent = new StringContent(responseStringJson);
                        newContent.Headers.Clear();
                        foreach (var header in response.Content.Headers)
                        {
                            newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                        response.Content = newContent;
                        if (accountId != null && responseObject.TryGetValue("deadline", StringComparison.OrdinalIgnoreCase, out var deadlineToken))
                        {
                            var deadline = deadlineToken.ToObject<ulong>();
                            var shares = _shareCalculator.GetShares(deadline, ulong.Parse(block.Height));
                            if (isPoolUser)
                            {
                                _logger.LogInformation($"{accountId} earned {shares} shares.");
                                await _shareTracker.RecordSharesAsync(accountId.Value, long.Parse(block.Height), shares, nonce, deadline).ConfigureAwait(false);
                            }
                            else
                            {
                                _logger.LogInformation($"{accountId} earned {shares} shares, but they are not a member of the pool. Not recording.");
                            }
                        }
                        return ResponseMessage(response);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed submit nonce to server, or failed to record shares.");
                        return BadGateway();
                    }
                default:
                    baseUri = baseUri == null ? GetProxyUri(requestType) : GetProxyUri(baseUri);
                    var request = CreateProxyHttpRequest(Request.HttpContext, baseUri);
                    try
                    {
                        var response = await _httpClient.SendAsync(request, Request.HttpContext.RequestAborted);
                        return ResponseMessage(response);
                    }
                    catch
                    {
                        return BadGateway();
                    }
            }
        }

        private IActionResult StatusCode(HttpStatusCode statusCode)
        {
            return StatusCode((int)statusCode);
        }

        private IActionResult BadGateway()
        {
            return StatusCode(HttpStatusCode.BadGateway);
        }

        private string GetPoolSecretPhrase()
        {
            return _configuration.GetSection("Pool")?.GetValue<string>("PoolSecretPhrase");
        }

        protected IActionResult ResponseMessage(HttpResponseMessage message)
        {
            return new ResponseMessageResult(message);
        }
        public class ResponseMessageResult : IActionResult
        {
            private HttpResponseMessage _responseMessage;
            public ResponseMessageResult(HttpResponseMessage responseMessage)
            {
                _responseMessage = responseMessage;
            }

            public Task ExecuteResultAsync(ActionContext context)
            {
                return CopyProxyHttpResponse(context.HttpContext, _responseMessage);
            }
        }

        private const int StreamCopyBufferSize = 81920;
        public static async Task CopyProxyHttpResponse(HttpContext context, HttpResponseMessage responseMessage)
        {
            if (responseMessage == null)
            {
                throw new ArgumentNullException(nameof(responseMessage));
            }

            var response = context.Response;

            response.StatusCode = (int)responseMessage.StatusCode;
            foreach (var header in responseMessage.Headers)
            {
                response.Headers[header.Key] = header.Value.ToArray();
            }

            foreach (var header in responseMessage.Content.Headers)
            {
                response.Headers[header.Key] = header.Value.ToArray();
            }

            // SendAsync removes chunking from the response. This removes the header so it doesn't expect a chunked response.
            response.Headers.Remove("transfer-encoding");

            using (var responseStream = await responseMessage.Content.ReadAsStreamAsync())
            {
                await responseStream.CopyToAsync(response.Body, StreamCopyBufferSize, context.RequestAborted);
            }
        }


        public static HttpRequestMessage CreateProxyHttpRequest(HttpContext context, Uri uri)
        {
            var request = context.Request;

            var requestMessage = new HttpRequestMessage();
            var requestMethod = request.Method;
            if (!HttpMethods.IsGet(requestMethod) &&
                !HttpMethods.IsHead(requestMethod) &&
                !HttpMethods.IsDelete(requestMethod) &&
                !HttpMethods.IsTrace(requestMethod))
            {
                var streamContent = new StreamContent(request.Body);
                requestMessage.Content = streamContent;
            }

            // Copy the request headers
            foreach (var header in request.Headers)
            {
                if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && requestMessage.Content != null)
                {
                    requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            requestMessage.Headers.Host = uri.Authority;
            requestMessage.RequestUri = uri;
            requestMessage.Method = new HttpMethod(request.Method);

            return requestMessage;
        }
    }
}