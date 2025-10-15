// Services/IAuditService.cs
using PCGroupCloningApp.Models;

namespace PCGroupCloningApp.Services
{
    public interface IAuditService
    {
        Task LogOperationAsync(string operation, string sourceComputer, string targetComputer,
            List<string> groupsCloned, List<string> additionalGroups, bool success,
            string? sourceComputerOU = null, string? sourceComputerOUDescription = null,
            string? targetComputerOU = null, string? targetComputerOUDescription = null,
            string? sourceComputerDescription = null, string? targetComputerDescription = null,
            string? errorMessage = null, string details = "");
        Task<List<AuditLog>> GetRecentLogsAsync(int count = 50);
        Task<List<AuditLog>> GetLogsByUserAsync(string username, int count = 50);
    }
}