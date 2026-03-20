// Pages/PCManagement.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PCGroupCloningApp.Services;

namespace PCGroupCloningApp.Pages
{
    public class PCManagementModel : PageModel
    {
        private readonly ILogger<PCManagementModel> _logger;

        public PCManagementModel(ILogger<PCManagementModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            // Simple page load - the real work happens via API calls
            _logger.LogInformation("PC Management page loaded by user: {User}", User.Identity?.Name);
        }
    }
}