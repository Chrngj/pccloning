// Services/AuditService.cs
using Microsoft.EntityFrameworkCore;
using PCGroupCloningApp.Data;
using PCGroupCloningApp.Models;

namespace PCGroupCloningApp.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditService> _logger;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<AuditService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogOperationAsync(string operation, string sourceComputer, string targetComputer,
    List<string> groupsCloned, List<string> additionalGroups, bool success,
    string? sourceComputerOU = null, string? sourceComputerOUDescription = null,
    string? targetComputerOU = null, string? targetComputerOUDescription = null,
    string? sourceComputerDescription = null, string? targetComputerDescription = null,
    string? errorMessage = null, string details = "")
        {
            try
            {
                var username = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Unknown";

                var auditLog = new AuditLog
                {
                    Timestamp = DateTime.Now,
                    Username = username,
                    Operation = operation,
                    SourceComputer = sourceComputer,
                    TargetComputer = targetComputer,
                    GroupsCloned = string.Join(", ", groupsCloned),
                    AdditionalGroups = string.Join(", ", additionalGroups),
                    Success = success,
                    SourceComputerOU = sourceComputerOU,
                    SourceComputerOUDescription = sourceComputerOUDescription,
                    TargetComputerOU = targetComputerOU,
                    TargetComputerOUDescription = targetComputerOUDescription,
                    SourceComputerDescription = sourceComputerDescription,
                    TargetComputerDescription = targetComputerDescription,
                    ErrorMessage = errorMessage,
                    Details = details
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit operation");
            }
        }

        public async Task<List<AuditLog>> GetRecentLogsAsync(int count = 50)
        {
            try
            {
                return await _context.AuditLogs
                    .OrderByDescending(a => a.Timestamp)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent logs");
                return new List<AuditLog>();
            }
        }

        public async Task<List<AuditLog>> GetLogsByUserAsync(string username, int count = 50)
        {
            try
            {
                return await _context.AuditLogs
                    .Where(a => a.Username == username)
                    .OrderByDescending(a => a.Timestamp)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving logs for user: {Username}", username);
                return new List<AuditLog>();
            }
        }
    }
}