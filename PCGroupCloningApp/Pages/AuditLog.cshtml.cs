// Pages/AuditLog.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PCGroupCloningApp.Data;
using PCGroupCloningApp.Models;
using System.ComponentModel.DataAnnotations;

namespace PCGroupCloningApp.Pages
{
    public class AuditLogModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuditLogModel> _logger;

        public AuditLogModel(ApplicationDbContext context, ILogger<AuditLogModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Filter Properties
        [BindProperty]
        public AuditFilters Filters { get; set; } = new();

        // Results
        public List<AuditLog> AuditLogs { get; set; } = new();
        public List<string> AvailableUsernames { get; set; } = new();
        public List<string> AvailableOperations { get; set; } = new();
        public int TotalRecords { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);

        // Filter Model
        public class AuditFilters
        {
            [Display(Name = "From Date")]
            [DataType(DataType.Date)]
            public DateTime? FromDate { get; set; }

            [Display(Name = "To Date")]
            [DataType(DataType.Date)]
            public DateTime? ToDate { get; set; }

            [Display(Name = "Username")]
            public string? Username { get; set; }

            [Display(Name = "Operation Type")]
            public string? Operation { get; set; }

            [Display(Name = "Source Computer")]
            public string? SourceComputer { get; set; }

            [Display(Name = "Target Computer")]
            public string? TargetComputer { get; set; }

            [Display(Name = "Error Messages")]
            public string? ErrorMessage { get; set; }

            public bool? Success { get; set; }

            [Display(Name = "Records per page")]
            public int PageSize { get; set; } = 25;

            [Display(Name = "Sort by")]
            public string SortBy { get; set; } = "Timestamp";

            [Display(Name = "Sort direction")]
            public string SortDirection { get; set; } = "desc";
        }

        public async Task OnGetAsync(int pageNumber = 1)
        {
            CurrentPage = pageNumber;
            PageSize = Filters.PageSize;

            await LoadFiltersDataAsync();
            await LoadAuditLogsAsync();
        }

        public async Task<IActionResult> OnPostAsync(int pageNumber = 1)
        {
            CurrentPage = pageNumber;
            PageSize = Filters.PageSize;

            await LoadFiltersDataAsync();
            await LoadAuditLogsAsync();
            return Page();
        }

        public async Task<IActionResult> OnGetDetailsAsync(int id)
        {
            try
            {
                var auditLog = await _context.AuditLogs.FindAsync(id);
                if (auditLog == null)
                {
                    return NotFound();
                }

                return new JsonResult(new
                {
                    success = true,
                    data = new
                    {
                        auditLog.Id,
                        auditLog.Timestamp,
                        auditLog.Username,
                        auditLog.Operation,
                        auditLog.SourceComputer,
                        auditLog.TargetComputer,
                        auditLog.SourceComputerOU,
                        auditLog.SourceComputerOUDescription,
                        auditLog.TargetComputerOU,
                        auditLog.TargetComputerOUDescription,
                        auditLog.SourceComputerDescription,
                        auditLog.TargetComputerDescription,
                        auditLog.GroupsCloned,
                        auditLog.AdditionalGroups,
                        auditLog.Success,
                        auditLog.ErrorMessage,
                        auditLog.Details
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit log details for ID: {Id}", id);
                return new JsonResult(new { success = false, message = "Error loading details" });
            }
        }

        private async Task LoadFiltersDataAsync()
        {
            try
            {
                // Get unique usernames
                AvailableUsernames = await _context.AuditLogs
                    .Select(a => a.Username)
                    .Distinct()
                    .Where(u => !string.IsNullOrEmpty(u))
                    .OrderBy(u => u)
                    .ToListAsync();

                // Get unique operations
                AvailableOperations = await _context.AuditLogs
                    .Select(a => a.Operation)
                    .Distinct()
                    .Where(o => !string.IsNullOrEmpty(o))
                    .OrderBy(o => o)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading filter data");
                AvailableUsernames = new List<string>();
                AvailableOperations = new List<string>();
            }
        }

        private async Task LoadAuditLogsAsync()
        {
            try
            {
                var query = _context.AuditLogs.AsQueryable();

                // Apply filters
                if (Filters.FromDate.HasValue)
                {
                    query = query.Where(a => a.Timestamp >= Filters.FromDate.Value);
                }

                if (Filters.ToDate.HasValue)
                {
                    // Add one day to include the entire "to" date
                    var toDate = Filters.ToDate.Value.AddDays(1);
                    query = query.Where(a => a.Timestamp < toDate);
                }

                if (!string.IsNullOrWhiteSpace(Filters.Username))
                {
                    query = query.Where(a => a.Username == Filters.Username);
                }

                if (!string.IsNullOrWhiteSpace(Filters.Operation))
                {
                    query = query.Where(a => a.Operation == Filters.Operation);
                }

                if (!string.IsNullOrWhiteSpace(Filters.SourceComputer))
                {
                    query = query.Where(a => a.SourceComputer.Contains(Filters.SourceComputer));
                }

                if (!string.IsNullOrWhiteSpace(Filters.TargetComputer))
                {
                    query = query.Where(a => a.TargetComputer.Contains(Filters.TargetComputer));
                }

                if (!string.IsNullOrWhiteSpace(Filters.ErrorMessage))
                {
                    query = query.Where(a => !string.IsNullOrEmpty(a.ErrorMessage) &&
                                           a.ErrorMessage.Contains(Filters.ErrorMessage));
                }

                if (Filters.Success.HasValue)
                {
                    query = query.Where(a => a.Success == Filters.Success.Value);
                }

                // Count total records for pagination
                TotalRecords = await query.CountAsync();

                // Apply sorting
                query = Filters.SortBy.ToLower() switch
                {
                    "username" => Filters.SortDirection == "asc"
                        ? query.OrderBy(a => a.Username)
                        : query.OrderByDescending(a => a.Username),
                    "operation" => Filters.SortDirection == "asc"
                        ? query.OrderBy(a => a.Operation)
                        : query.OrderByDescending(a => a.Operation),
                    "sourcecomputer" => Filters.SortDirection == "asc"
                        ? query.OrderBy(a => a.SourceComputer)
                        : query.OrderByDescending(a => a.SourceComputer),
                    "targetcomputer" => Filters.SortDirection == "asc"
                        ? query.OrderBy(a => a.TargetComputer)
                        : query.OrderByDescending(a => a.TargetComputer),
                    "success" => Filters.SortDirection == "asc"
                        ? query.OrderBy(a => a.Success)
                        : query.OrderByDescending(a => a.Success),
                    _ => Filters.SortDirection == "asc"
                        ? query.OrderBy(a => a.Timestamp)
                        : query.OrderByDescending(a => a.Timestamp)
                };

                // Apply pagination
                var skip = (CurrentPage - 1) * PageSize;
                AuditLogs = await query.Skip(skip).Take(PageSize).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading audit logs");
                AuditLogs = new List<AuditLog>();
                TotalRecords = 0;
            }
        }

        public string GetSortIcon(string columnName)
        {
            if (Filters.SortBy.Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return Filters.SortDirection == "asc" ? "fa-sort-up" : "fa-sort-down";
            }
            return "fa-sort";
        }

        public string GetNextSortDirection(string columnName)
        {
            if (Filters.SortBy.Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return Filters.SortDirection == "asc" ? "desc" : "asc";
            }
            return "asc";
        }
    }
}