// Api/CloneController.cs
using Microsoft.AspNetCore.Mvc;
using PCGroupCloningApp.Services;

namespace PCGroupCloningApp.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class CloneController : ControllerBase
    {
        private readonly IActiveDirectoryService _adService;
        private readonly IAuditService _auditService;
        private readonly ILogger<CloneController> _logger;

        public CloneController(IActiveDirectoryService adService, IAuditService auditService, ILogger<CloneController> logger)
        {
            _adService = adService;
            _auditService = auditService;
            _logger = logger;
        }

        [HttpPost("execute")]
        public async Task<IActionResult> ExecuteClone([FromBody] CloneRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var successCount = 0;
            var errorCount = 0;
            var errors = new List<string>();
            var operations = new List<string>();

            try
            {
                _logger.LogInformation("Starting clone operation: {Source} → {Target}",
                    request.SourceComputer, request.TargetComputer);

                // Step 1: Get current groups on target computer
                var targetCurrentGroups = await _adService.GetComputerGroupsDetailedAsync(request.TargetComputer);
                _logger.LogInformation("Target computer {Target} currently has {GroupCount} groups: {Groups}",
                    request.TargetComputer, targetCurrentGroups.Count, string.Join(", ", targetCurrentGroups));

                // Step 2: Determine which groups to remove (all except system groups)
                var systemGroups = new[] { "Domain Users", "Domain Computers" };
                var groupsToRemove = targetCurrentGroups.Where(g => !systemGroups.Contains(g)).ToList();

                if (groupsToRemove.Any())
                {
                    _logger.LogInformation("Removing {GroupCount} non-system groups from target computer {Target}: {Groups}",
                        groupsToRemove.Count, request.TargetComputer, string.Join(", ", groupsToRemove));

                    var removeSuccess = await _adService.RemoveComputerFromMultipleGroupsAsync(request.TargetComputer, groupsToRemove);

                    if (!removeSuccess)
                    {
                        errors.Add("Failed to remove existing groups from target computer - operation stopped");
                        await LogFailedOperation(request, errors, "Group removal failed");
                        return Ok(new { success = false, errors = errors, message = "Operation failed during group removal" });
                    }

                    operations.Add($"Removed {groupsToRemove.Count} existing groups");
                }
                else
                {
                    _logger.LogInformation("No non-system groups to remove from target computer {Target}", request.TargetComputer);
                    operations.Add("No existing groups to remove");
                }

                // Step 3: Prepare groups to add (with Office conversion)
                var allGroupsToAdd = request.SelectedGroups.Concat(request.AdditionalGroups).ToList();
                var convertedGroups = ConvertOfficeGroups(allGroupsToAdd, request.TargetComputer);

                _logger.LogInformation("Adding {GroupCount} groups to target computer {Target}: {Groups}",
                    convertedGroups.Count, request.TargetComputer, string.Join(", ", convertedGroups));

                // Step 4: Add groups to target computer
                foreach (var groupName in convertedGroups)
                {
                    var success = await _adService.AddComputerToGroupAsync(request.TargetComputer, groupName);
                    if (success)
                    {
                        successCount++;
                        _logger.LogInformation("Successfully added {Target} to group {Group} ({Current}/{Total})",
                            request.TargetComputer, groupName, successCount, convertedGroups.Count);
                    }
                    else
                    {
                        errorCount++;
                        var error = $"Failed to add to group: {groupName}";
                        errors.Add(error);
                        _logger.LogError("Failed to add {Target} to group {Group}", request.TargetComputer, groupName);
                    }
                }

                // Step 5: Move to same OU if requested
                if (request.MoveToSameOU && !string.IsNullOrEmpty(request.SourceComputerOU))
                {
                    _logger.LogInformation("Moving target computer {Target} to same OU as source: {OU}",
                        request.TargetComputer, request.SourceComputerOU);

                    var ouMoveSuccess = await _adService.MoveComputerToOUAsync(request.TargetComputer, request.SourceComputerOU);
                    if (ouMoveSuccess)
                    {
                        operations.Add("Moved to source OU");
                        _logger.LogInformation("Successfully moved {Target} to OU: {OU}", request.TargetComputer, request.SourceComputerOU);
                    }
                    else
                    {
                        errorCount++;
                        errors.Add("Failed to move computer to source OU");
                        _logger.LogError("Failed to move {Target} to OU: {OU}", request.TargetComputer, request.SourceComputerOU);
                    }
                }

                // Step 6: Log the operation
                var isSuccess = errorCount == 0;
                await _auditService.LogOperationAsync(
                    "Clone Groups (Enhanced)",
                    request.SourceComputer,
                    request.TargetComputer,
                    request.SelectedGroups,
                    request.AdditionalGroups,
                    isSuccess,
                    errorCount > 0 ? string.Join("; ", errors) : null,
                    $"Operations: {string.Join(", ", operations)}. Success: {successCount}, Errors: {errorCount}. Groups removed: {groupsToRemove.Count}"
                );

                var message = isSuccess
                    ? $"Successfully completed clone operation! Added {successCount} groups."
                    : $"Clone completed with {successCount} successes and {errorCount} errors";

                return Ok(new
                {
                    success = isSuccess,
                    successCount = successCount,
                    errorCount = errorCount,
                    errors = errors,
                    operations = operations,
                    message = message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Clone operation failed with exception");

                await _auditService.LogOperationAsync(
                    "Clone Groups (Enhanced)",
                    request.SourceComputer,
                    request.TargetComputer,
                    request.SelectedGroups,
                    request.AdditionalGroups,
                    false,
                    ex.Message,
                    "Operation failed with exception"
                );

                return StatusCode(500, new { success = false, message = "Clone operation failed", error = ex.Message });
            }
        }

        private async Task LogFailedOperation(CloneRequest request, List<string> errors, string details)
        {
            await _auditService.LogOperationAsync(
                "Clone Groups (Enhanced)",
                request.SourceComputer,
                request.TargetComputer,
                request.SelectedGroups,
                request.AdditionalGroups,
                false,
                string.Join("; ", errors),
                details
            );
        }

        private List<string> ConvertOfficeGroups(List<string> sourceGroups, string targetComputerName)
        {
            var convertedGroups = new List<string>();

            // Check if target computer has A/a in name
            bool shouldUpgradeOffice = targetComputerName.ToLower().Contains('a');

            var officeGroupMappings = new Dictionary<string, string>
            {
                ["LSS-App-Office-Professional-2021-Academic"] = "LSS-App-Office-Professional",
                ["LSS-App-Office-Professional-2021-Corporate"] = "LSS-App-Office-Professional",
                ["LSS-App-Office-Standard-2021-Academic"] = "LSS-App-Office-Standard",
                ["LSS-App-Office-Standard-2021-Corporate"] = "LSS-App-Office-Standard"
            };

            foreach (var group in sourceGroups)
            {
                if (shouldUpgradeOffice && officeGroupMappings.ContainsKey(group))
                {
                    var newGroup = officeGroupMappings[group];
                    convertedGroups.Add(newGroup);
                    _logger.LogInformation("Office group conversion: {OldGroup} → {NewGroup} for computer {Computer}",
                        group, newGroup, targetComputerName);
                }
                else
                {
                    convertedGroups.Add(group);
                }
            }

            return convertedGroups.Distinct().ToList(); // Remove duplicates
        }

        public class CloneRequest
        {
            public string SourceComputer { get; set; } = string.Empty;
            public string TargetComputer { get; set; } = string.Empty;
            public List<string> SelectedGroups { get; set; } = new();
            public List<string> AdditionalGroups { get; set; } = new();
            public bool MoveToSameOU { get; set; }
            public string SourceComputerOU { get; set; } = string.Empty;
        }
    }
}