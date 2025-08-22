// Api/ComputerController.cs
using Microsoft.AspNetCore.Mvc;
using PCGroupCloningApp.Services;

namespace PCGroupCloningApp.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class ComputerController : ControllerBase
    {
        private readonly IActiveDirectoryService _adService;
        private readonly ILogger<ComputerController> _logger;

        public ComputerController(IActiveDirectoryService adService, ILogger<ComputerController> logger)
        {
            _adService = adService;
            _logger = logger;
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchComputers([FromQuery] string term)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            {
                return Ok(new List<string>());
            }

            try
            {
                var computers = await _adService.SearchComputersAsync(term);
                return Ok(computers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching computers");
                return StatusCode(500, "Error searching computers");
            }
        }

        [HttpGet("{computerName}/groups")]
        public async Task<IActionResult> GetComputerGroups(string computerName)
        {
            if (string.IsNullOrWhiteSpace(computerName))
            {
                return BadRequest("Computer name is required");
            }

            try
            {
                var groups = await _adService.GetComputerGroupsAsync(computerName);
                return Ok(groups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting groups for computer: {ComputerName}", computerName);
                return StatusCode(500, "Error getting computer groups");
            }
        }

        [HttpGet("{computerName}/ou")]
        public async Task<IActionResult> GetComputerOU(string computerName)
        {
            if (string.IsNullOrWhiteSpace(computerName))
            {
                return BadRequest("Computer name is required");
            }

            try
            {
                var ou = await _adService.GetComputerOUAsync(computerName);
                return Ok(new { ou = ou });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting OU for computer: {ComputerName}", computerName);
                return StatusCode(500, "Error getting computer OU");
            }
        }
        [HttpGet("debug/currentuser")]
        public IActionResult GetCurrentUser()
        {
            return Ok(new
            {
                EnvironmentUser = Environment.UserName,
                EnvironmentDomain = Environment.UserDomainName,
                WindowsIdentity = System.Security.Principal.WindowsIdentity.GetCurrent().Name,
                HttpContextUser = User.Identity?.Name,
                IsAuthenticated = User.Identity?.IsAuthenticated
            });
        }
    }
}