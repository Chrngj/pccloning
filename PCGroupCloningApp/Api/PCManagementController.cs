// Api/PCManagementController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PCGroupCloningApp.Data;
using PCGroupCloningApp.Models;
using PCGroupCloningApp.Services;

namespace PCGroupCloningApp.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class PCManagementController : ControllerBase
    {
        private readonly IActiveDirectoryService _adService;
        private readonly IAuditService _auditService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PCManagementController> _logger;
        private const string NYRULLET_OU = "OU=NyRullet,DC=ibk,DC=lan";

        public PCManagementController(
            IActiveDirectoryService adService,
            IAuditService auditService,
            ApplicationDbContext context,
            ILogger<PCManagementController> logger)
        {
            _adService = adService;
            _auditService = auditService;
            _context = context;
            _logger = logger;
        }

        [HttpPost("reset")]
        public async Task<IActionResult> ResetComputer([FromBody] ResetRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ComputerName))
            {
                return BadRequest(new { success = false, message = "Computer name is required" });
            }

            var username = User.Identity?.Name ?? "Unknown";
            _logger.LogInformation("User {User} initiating reset for computer {Computer}",
                username, request.ComputerName);

            try
            {
                // Get current OU for logging
                var currentOU = await _adService.GetComputerOUAsync(request.ComputerName);
                _logger.LogInformation("Computer {Computer} current OU: {CurrentOU}",
                    request.ComputerName, currentOU);

                // Move computer to NyRullet OU
                var success = await _adService.MoveComputerToOUAsync(request.ComputerName, NYRULLET_OU);

                if (success)
                {
                    _logger.LogInformation("Successfully moved {Computer} to NyRullet OU",
                        request.ComputerName);

                    // Log to audit
                    await _auditService.LogOperationAsync(
                        "Move to NyRullet",
                        request.ComputerName,  // Using SourceComputer field for the computer being moved
                        "NyRullet",            // Using TargetComputer field to indicate destination
                        new List<string>(),    // No groups changed
                        new List<string>(),    // No additional groups
                        true,
                        null,
                        $"Moved from: {currentOU}. Reset operation successful."
                    );

                    return Ok(new
                    {
                        success = true,
                        message = $"Successfully reset {request.ComputerName} and moved to NyRullet OU",
                        previousOU = currentOU
                    });
                }
                else
                {
                    _logger.LogError("Failed to move {Computer} to NyRullet OU", request.ComputerName);

                    // Log failed attempt to audit
                    await _auditService.LogOperationAsync(
                        "Move to NyRullet",
                        request.ComputerName,
                        "NyRullet",
                        new List<string>(),
                        new List<string>(),
                        false,
                        "Failed to move computer to NyRullet OU",
                        $"Attempted to move from: {currentOU}"
                    );

                    return Ok(new
                    {
                        success = false,
                        message = "Failed to move computer to NyRullet OU. Please check permissions and try again."
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting computer {Computer}", request.ComputerName);

                // Log exception to audit
                await _auditService.LogOperationAsync(
                    "Move to NyRullet",
                    request.ComputerName,
                    "NyRullet",
                    new List<string>(),
                    new List<string>(),
                    false,
                    ex.Message,
                    "Exception occurred during reset operation"
                );

                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error occurred while resetting computer: {ex.Message}"
                });
            }
        }

        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentOperations()
        {
            try
            {
                // Get recent PC Management operations from audit log
                var recentOps = await _context.AuditLogs
                    .Where(a => a.Operation == "Move to NyRullet")
                    .OrderByDescending(a => a.Timestamp)
                    .Take(10)
                    .Select(a => new
                    {
                        a.Timestamp,
                        a.Username,
                        a.Operation,
                        a.SourceComputer,  // The computer that was moved
                        TargetComputer = a.TargetComputer, // Will show "NyRullet"
                        a.Success,
                        a.ErrorMessage,
                        a.Details
                    })
                    .ToListAsync();

                // Transform for display
                var result = recentOps.Select(op => new
                {
                    timestamp = op.Timestamp,
                    username = op.Username,
                    operation = op.Operation,
                    targetComputer = op.SourceComputer, // Show the actual computer name
                    success = op.Success,
                    errorMessage = op.ErrorMessage,
                    details = op.Details
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent PC Management operations");
                return StatusCode(500, new List<object>());
            }
        }

        // Request model
        public class ResetRequest
        {
            public string ComputerName { get; set; } = string.Empty;
        }
    }
}