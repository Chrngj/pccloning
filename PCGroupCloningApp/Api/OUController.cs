// Api/OUController.cs
using Microsoft.AspNetCore.Mvc;
using PCGroupCloningApp.Services;

namespace PCGroupCloningApp.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class OUController : ControllerBase
    {
        private readonly IOUService _ouService;
        private readonly ILogger<OUController> _logger;

        public OUController(IOUService ouService, ILogger<OUController> logger)
        {
            _ouService = ouService;
            _logger = logger;
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchOUs([FromQuery] string term)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            {
                return Ok(new List<string>());
            }

            try
            {
                var ous = await _ouService.SearchOUsAsync(term);
                return Ok(ous);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching OUs");
                return StatusCode(500, "Error searching OUs");
            }
        }
    }
}