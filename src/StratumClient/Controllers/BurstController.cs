using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using StratumClient.Services.Interfaces;

namespace StratumClient.Controllers
{
    [Route("burst")]
    public class BurstController : Controller
    {
        private readonly IStratum _stratum;
        public BurstController(IStratum stratum) {
            _stratum = stratum;
        }   
        public async Task<IActionResult> GetAsync(string requestType)
        {
            switch (requestType)
            {
                case "getMiningInfo":
                    return Ok(await _stratum.GetMiningInfoAsync(HttpContext.RequestAborted));
                default:
                    return BadRequest(new
                    {
                        error = $"The stratum proxy only supports getMiningInfo, you sent requestType: {requestType}"
                    });
            }
        }
    }
}
