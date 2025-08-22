// Api/GroupsController.cs
using Microsoft.AspNetCore.Mvc;
using PCGroupCloningApp.Services;

namespace PCGroupCloningApp.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class GroupsController : ControllerBase
    {
        private readonly IActiveDirectoryService _adService;
        private readonly ILogger<GroupsController> _logger;

        public GroupsController(IActiveDirectoryService adService, ILogger<GroupsController> logger)
        {
            _adService = adService;
            _logger = logger;
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchGroups([FromQuery] string term)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            {
                return Ok(new List<string>());
            }

            try
            {
                var groups = await _adService.SearchGroupsAsync(term);
                return Ok(groups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching groups");
                return StatusCode(500, "Error searching groups");
            }
        }
    }
}