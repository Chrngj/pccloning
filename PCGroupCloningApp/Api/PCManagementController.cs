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
        private readonly ISCCMService _sccmService;
        private readonly IAuditService _auditService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PCManagementController> _logger;
        private const string NYRULLET_OU = "OU=NyRullet,DC=ibk,DC=lan";
        private const string STOLEN_OU = "OU=Stjålet,OU=UdskiftedeComputere,DC=ibk,DC=lan";

        public PCManagementController(
            IActiveDirectoryService adService,
            ISCCMService sccmService,
            IAuditService auditService,
            ApplicationDbContext context,
            ILogger<PCManagementController> logger)
        {
            _adService = adService;
            _sccmService = sccmService;
            _auditService = auditService;
            _context = context;
            _logger = logger;
        }

        // =====================================================
        // EXISTING: Move to NyRullet
        // =====================================================
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
                var currentOU = await _adService.GetComputerOUAsync(request.ComputerName);
                _logger.LogInformation("Computer {Computer} current OU: {CurrentOU}",
                    request.ComputerName, currentOU);

                var protectedGroups = new[] { "ADAuditPlusWS", "Domain Computers" };
                var currentGroups = await _adService.GetComputerGroupsDetailedAsync(request.ComputerName);
                var groupsToRemove = currentGroups.Where(g => !protectedGroups.Contains(g)).ToList();

                if (groupsToRemove.Any())
                {
                    _logger.LogInformation("Removing {Count} groups from {Computer}: {Groups}",
                        groupsToRemove.Count, request.ComputerName, string.Join(", ", groupsToRemove));

                    var removeSuccess = await _adService.RemoveComputerFromMultipleGroupsAsync(request.ComputerName, groupsToRemove);
                    if (!removeSuccess)
                    {
                        _logger.LogError("Failed to remove groups from {Computer}", request.ComputerName);

                        await _auditService.LogOperationAsync(
                            "Move to NyRullet",
                            request.ComputerName,
                            "NyRullet",
                            new List<string>(),
                            new List<string>(),
                            false,
                            "Failed to remove groups from computer",
                            $"Attempted to remove {groupsToRemove.Count} groups. Groups: {string.Join(", ", groupsToRemove)}"
                        );

                        return Ok(new
                        {
                            success = false,
                            message = "Failed to remove groups from computer. Computer was NOT moved."
                        });
                    }
                }

                var success = await _adService.MoveComputerToOUAsync(request.ComputerName, NYRULLET_OU);
                if (success)
                {
                    _logger.LogInformation("Successfully moved {Computer} to NyRullet OU", request.ComputerName);

                    await _auditService.LogOperationAsync(
                        "Move to NyRullet",
                        request.ComputerName,
                        "NyRullet",
                        new List<string>(),
                        new List<string>(),
                        true,
                        null,
                        $"Moved from: {currentOU}. Removed {groupsToRemove.Count} groups. Reset operation successful."
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

        // =====================================================
        // NEW: Delete PC (SCCM + AD)
        // =====================================================
        [HttpPost("delete")]
        public async Task<IActionResult> DeleteComputer([FromBody] DeleteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ComputerName))
            {
                return BadRequest(new { success = false, message = "Computer name is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                return BadRequest(new { success = false, message = "Description is required" });
            }

            var username = User.Identity?.Name ?? "Unknown";
            _logger.LogInformation("User {User} initiating DELETE for computer {Computer} (Stolen: {Stolen}, Description: {Description})",
                username, request.ComputerName, request.IsStolen, request.Description);

            var operations = new List<string>();
            var errors = new List<string>();

            // Collect info before deletion for audit logging
            string adOU = "";
            string adOUDescription = "";
            string computerDescription = "";
            List<string> adGroups = new();
            List<SCCMCollectionInfo> sccmOSDCollections = new();
            int? sccmResourceId = null;

            try
            {
                // ---- Step 1: Gather info from AD ----
                try
                {
                    var (ou, ouDesc, compDesc) = await _adService.GetComputerDetailsAsync(request.ComputerName);
                    adOU = ou;
                    adOUDescription = ouDesc;
                    computerDescription = compDesc;
                    adGroups = await _adService.GetComputerGroupsDetailedAsync(request.ComputerName);
                    operations.Add($"AD info gathered: OU={adOU}, Groups={adGroups.Count}");
                    _logger.LogInformation("Delete: AD info for {Computer} - OU: {OU}, Groups: {GroupCount}",
                        request.ComputerName, adOU, adGroups.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Delete: Could not gather AD info for {Computer}", request.ComputerName);
                    operations.Add("AD info: Computer not found in AD or error retrieving info");
                }

                // ---- Step 2: Gather info from SCCM ----
                try
                {
                    sccmResourceId = await _sccmService.GetDeviceResourceIdAsync(request.ComputerName);
                    if (sccmResourceId.HasValue)
                    {
                        sccmOSDCollections = await _sccmService.GetDeviceOSDCollectionsAsync(sccmResourceId.Value);
                        operations.Add($"SCCM info gathered: ResourceID={sccmResourceId.Value}, OSD Collections={sccmOSDCollections.Count}");
                        _logger.LogInformation("Delete: SCCM info for {Computer} - ResourceID: {ResourceId}, OSD Collections: {Count}",
                            request.ComputerName, sccmResourceId.Value, sccmOSDCollections.Count);
                    }
                    else
                    {
                        operations.Add("SCCM info: Device not found in SCCM");
                        _logger.LogInformation("Delete: {Computer} not found in SCCM", request.ComputerName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Delete: Could not gather SCCM info for {Computer}", request.ComputerName);
                    operations.Add("SCCM info: Error retrieving info");
                }

                // ---- Step 3: Delete from SCCM ----
                if (sccmResourceId.HasValue)
                {
                    var sccmDeleted = await _sccmService.DeleteDeviceAsync(sccmResourceId.Value);
                    if (sccmDeleted)
                    {
                        operations.Add("SCCM: Device deleted successfully");
                        _logger.LogInformation("Delete: Successfully deleted {Computer} from SCCM", request.ComputerName);
                    }
                    else
                    {
                        errors.Add("Failed to delete device from SCCM");
                        operations.Add("SCCM: Failed to delete device");
                        _logger.LogError("Delete: Failed to delete {Computer} from SCCM", request.ComputerName);
                    }
                }

                // ---- Step 4: Handle AD (delete or move to stolen OU) ----
                bool adHandled = false;
                if (!string.IsNullOrEmpty(adOU) || adGroups.Any()) // Computer exists in AD
                {
                    if (request.IsStolen)
                    {
                        // Move to stolen OU
                        var moveSuccess = await _adService.MoveComputerToOUAsync(request.ComputerName, STOLEN_OU);
                        if (moveSuccess)
                        {
                            operations.Add($"AD: Computer moved to Stjålet OU");
                            adHandled = true;
                            _logger.LogInformation("Delete: Moved stolen {Computer} to {OU}", request.ComputerName, STOLEN_OU);
                        }
                        else
                        {
                            errors.Add("Failed to move computer to Stjålet OU");
                            operations.Add("AD: Failed to move to Stjålet OU");
                            _logger.LogError("Delete: Failed to move {Computer} to Stjålet OU", request.ComputerName);
                        }
                    }
                    else
                    {
                        // Delete from AD
                        var deleteSuccess = await _adService.DeleteComputerAsync(request.ComputerName);
                        if (deleteSuccess)
                        {
                            operations.Add("AD: Computer deleted successfully");
                            adHandled = true;
                            _logger.LogInformation("Delete: Successfully deleted {Computer} from AD", request.ComputerName);
                        }
                        else
                        {
                            errors.Add("Failed to delete computer from AD");
                            operations.Add("AD: Failed to delete computer");
                            _logger.LogError("Delete: Failed to delete {Computer} from AD", request.ComputerName);
                        }
                    }
                }
                else
                {
                    operations.Add("AD: Computer not found in AD - skipped");
                }

                // ---- Step 5: Log to audit ----
                var isSuccess = !errors.Any();
                var sccmCollectionNames = sccmOSDCollections.Any()
                    ? string.Join(", ", sccmOSDCollections.Select(c => $"{c.Name} ({c.CollectionID})"))
                    : "None";

                await _auditService.LogOperationAsync(
                    request.IsStolen ? "Delete PC (Stolen)" : "Delete PC",
                    request.ComputerName,
                    request.IsStolen ? "Stjålet OU" : "Deleted",
                    adGroups,
                    new List<string>(),
                    isSuccess,
                    adOU,
                    adOUDescription,
                    null, // targetOU
                    null, // targetOUDescription
                    computerDescription,
                    null, // targetComputerDescription
                    errors.Any() ? string.Join("; ", errors) : null,
                    $"Description: {request.Description}. " +
                    $"Stolen: {request.IsStolen}. " +
                    $"SCCM ResourceID: {sccmResourceId?.ToString() ?? "N/A"}. " +
                    $"SCCM OSD Collections: {sccmCollectionNames}. " +
                    $"Operations: {string.Join(", ", operations)}"
                );

                return Ok(new
                {
                    success = isSuccess,
                    message = isSuccess
                        ? $"Successfully deleted {request.ComputerName}" + (request.IsStolen ? " (moved to Stjålet OU in AD)" : "")
                        : $"Delete completed with errors: {string.Join(", ", errors)}",
                    operations,
                    errors,
                    sccmFound = sccmResourceId.HasValue,
                    adFound = !string.IsNullOrEmpty(adOU) || adGroups.Any()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete: Exception during delete of {Computer}", request.ComputerName);

                await _auditService.LogOperationAsync(
                    request.IsStolen ? "Delete PC (Stolen)" : "Delete PC",
                    request.ComputerName,
                    request.IsStolen ? "Stjålet OU" : "Deleted",
                    adGroups,
                    new List<string>(),
                    false,
                    adOU,
                    adOUDescription,
                    null, null, computerDescription, null,
                    ex.Message,
                    $"Description: {request.Description}. Exception during delete. Operations before error: {string.Join(", ", operations)}"
                );

                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error occurred during delete: {ex.Message}"
                });
            }
        }

        // =====================================================
        // NEW: Get SCCM info for a computer (for UI display)
        // =====================================================
        [HttpGet("{computerName}/sccminfo")]
        public async Task<IActionResult> GetSCCMInfo(string computerName)
        {
            try
            {
                var resourceId = await _sccmService.GetDeviceResourceIdAsync(computerName);
                if (!resourceId.HasValue)
                {
                    return Ok(new { found = false, message = "Computer not found in SCCM" });
                }

                var osdCollections = await _sccmService.GetDeviceOSDCollectionsAsync(resourceId.Value);

                // Find current role
                var moveableRoles = new[] { "PS30062F", "PS300643", "PS300645", "PS3006C8", "PS300666" };
                var noRoleId = "PS300660";
                var currentRole = osdCollections.FirstOrDefault(c => moveableRoles.Contains(c.CollectionID));
                var hasNoRole = osdCollections.Any(c => c.CollectionID == noRoleId);

                return Ok(new
                {
                    found = true,
                    resourceId = resourceId.Value,
                    osdCollections = osdCollections.Select(c => new { c.CollectionID, c.Name }),
                    currentRole = currentRole != null ? new { currentRole.CollectionID, currentRole.Name } : null,
                    hasNoRole = hasNoRole
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SCCM info for {ComputerName}", computerName);
                return StatusCode(500, new { found = false, message = "Error retrieving SCCM information" });
            }
        }

        // =====================================================
        // NEW: Get available OSD Role collections
        // =====================================================
        [HttpGet("osdroles")]
        public async Task<IActionResult> GetOSDRoles()
        {
            var roles = await _sccmService.GetAllOSDRoleCollectionsAsync();
            return Ok(roles);
        }

        // =====================================================
        // NEW: Move OSD Role
        // =====================================================
        [HttpPost("moveosd")]
        public async Task<IActionResult> MoveOSDRole([FromBody] MoveOSDRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ComputerName))
            {
                return BadRequest(new { success = false, message = "Computer name is required" });
            }

            if (string.IsNullOrWhiteSpace(request.TargetCollectionId))
            {
                return BadRequest(new { success = false, message = "Target OSD Role is required" });
            }

            var username = User.Identity?.Name ?? "Unknown";
            _logger.LogInformation("User {User} initiating OSD Role move for {Computer} to {TargetCollection}",
                username, request.ComputerName, request.TargetCollectionId);

            var operations = new List<string>();
            var errors = new List<string>();

            try
            {
                // Step 1: Find device in SCCM
                var resourceId = await _sccmService.GetDeviceResourceIdAsync(request.ComputerName);
                if (!resourceId.HasValue)
                {
                    return Ok(new { success = false, message = "Computer not found in SCCM" });
                }

                // Step 2: Get current OSD collections
                var currentOSD = await _sccmService.GetDeviceOSDCollectionsAsync(resourceId.Value);
                var moveableRoles = new[] { "PS30062F", "PS300643", "PS300645", "PS3006C8", "PS300666" };
                var noRoleId = "PS300660";
                var currentRole = currentOSD.FirstOrDefault(c => moveableRoles.Contains(c.CollectionID));
                var hasNoRole = currentOSD.Any(c => c.CollectionID == noRoleId);

                // Step 3: Remove from current role (if any - but NEVER remove from "No Role assigned")
                if (currentRole != null)
                {
                    if (currentRole.CollectionID == request.TargetCollectionId)
                    {
                        return Ok(new { success = true, message = $"Computer is already in {currentRole.Name}" });
                    }

                    var removeSuccess = await _sccmService.RemoveDeviceFromCollectionAsync(
                        resourceId.Value, request.ComputerName, currentRole.CollectionID);

                    if (removeSuccess)
                    {
                        operations.Add($"Removed from {currentRole.Name} ({currentRole.CollectionID})");
                    }
                    else
                    {
                        errors.Add($"Failed to remove from {currentRole.Name}");
                    }
                }
                else if (hasNoRole)
                {
                    // PC is in "No Role assigned" - just add to new role, don't remove from No Role
                    operations.Add("Currently in 'No Role assigned' (dynamic - not removed)");
                }
                else
                {
                    operations.Add("No current role found");
                }

                // Step 4: Add to new role
                var addSuccess = await _sccmService.AddDeviceToCollectionAsync(
                    resourceId.Value, request.ComputerName, request.TargetCollectionId);

                if (addSuccess)
                {
                    var targetRoleName = (await _sccmService.GetAllOSDRoleCollectionsAsync())
                        .FirstOrDefault(r => r.CollectionID == request.TargetCollectionId)?.Name ?? request.TargetCollectionId;
                    operations.Add($"Added to {targetRoleName} ({request.TargetCollectionId})");
                }
                else
                {
                    errors.Add($"Failed to add to collection {request.TargetCollectionId}");
                }

                // Step 5: Log to audit
                var isSuccess = !errors.Any();
                var targetRole = (await _sccmService.GetAllOSDRoleCollectionsAsync())
                    .FirstOrDefault(r => r.CollectionID == request.TargetCollectionId);

                await _auditService.LogOperationAsync(
                    "Move OSD Role",
                    request.ComputerName,
                    targetRole?.Name ?? request.TargetCollectionId,
                    new List<string>(),
                    new List<string>(),
                    isSuccess,
                    currentRole?.Name ?? "No previous role",
                    currentRole?.CollectionID ?? "",
                    targetRole?.Name ?? "",
                    request.TargetCollectionId,
                    null, null,
                    errors.Any() ? string.Join("; ", errors) : null,
                    $"SCCM ResourceID: {resourceId.Value}. " +
                    $"Previous role: {currentRole?.Name ?? "None"} ({currentRole?.CollectionID ?? "N/A"}). " +
                    $"New role: {targetRole?.Name ?? "Unknown"} ({request.TargetCollectionId}). " +
                    $"Operations: {string.Join(", ", operations)}"
                );

                return Ok(new
                {
                    success = isSuccess,
                    message = isSuccess
                        ? $"Successfully moved {request.ComputerName} to {targetRole?.Name ?? request.TargetCollectionId}"
                        : $"Move completed with errors: {string.Join(", ", errors)}",
                    operations,
                    errors,
                    previousRole = currentRole?.Name,
                    newRole = targetRole?.Name
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MoveOSD: Exception for {Computer}", request.ComputerName);

                await _auditService.LogOperationAsync(
                    "Move OSD Role",
                    request.ComputerName,
                    request.TargetCollectionId,
                    new List<string>(),
                    new List<string>(),
                    false,
                    null, null, null, null, null, null,
                    ex.Message,
                    $"Exception during OSD role move. Operations before error: {string.Join(", ", operations)}"
                );

                return StatusCode(500, new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // =====================================================
        // EXISTING: Recent operations (updated to include new operation types)
        // =====================================================
        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentOperations()
        {
            try
            {
                var pcMgmtOperations = new[] { "Move to NyRullet", "Delete PC", "Delete PC (Stolen)", "Move OSD Role" };

                var recentOps = await _context.AuditLogs
                    .Where(a => pcMgmtOperations.Contains(a.Operation))
                    .OrderByDescending(a => a.Timestamp)
                    .Take(15)
                    .Select(a => new
                    {
                        a.Timestamp,
                        a.Username,
                        a.Operation,
                        a.SourceComputer,
                        a.TargetComputer,
                        a.Success,
                        a.ErrorMessage,
                        a.Details
                    })
                    .ToListAsync();

                var result = recentOps.Select(op => new
                {
                    timestamp = op.Timestamp,
                    username = op.Username,
                    operation = op.Operation,
                    targetComputer = op.SourceComputer,
                    destination = op.TargetComputer,
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

        // =====================================================
        // Request Models
        // =====================================================
        public class ResetRequest
        {
            public string ComputerName { get; set; } = string.Empty;
        }

        public class DeleteRequest
        {
            public string ComputerName { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public bool IsStolen { get; set; } = false;
        }

        public class MoveOSDRequest
        {
            public string ComputerName { get; set; } = string.Empty;
            public string TargetCollectionId { get; set; } = string.Empty;
        }
    }
}