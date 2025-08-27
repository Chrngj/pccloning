// Pages/Admin/Management.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PCGroupCloningApp.Data;
using PCGroupCloningApp.Models;
using PCGroupCloningApp.Services;
using System.ComponentModel.DataAnnotations;

namespace PCGroupCloningApp.Pages.Admin
{
    public class ManagementModel : PageModel
    {
        private readonly IServiceAccountService _serviceAccountService;
        private readonly IOUService _ouService;
        private readonly ILogger<ManagementModel> _logger;

        public ManagementModel(IServiceAccountService serviceAccountService, IOUService ouService, ILogger<ManagementModel> logger)
        {
            _serviceAccountService = serviceAccountService;
            _ouService = ouService;
            _logger = logger;
        }

        [BindProperty]
        public ServiceAccountInput ServiceAccount { get; set; } = new();

        [BindProperty]
        public OUConfigurationInput OUConfig { get; set; } = new();

        public string? StatusMessage { get; set; }
        public bool HasExistingAccount { get; set; }
        public string? CurrentRetiredOU { get; set; }

        public class ServiceAccountInput
        {
            [Required]
            [Display(Name = "Domain")]
            public string Domain { get; set; } = "IBK.lan";

            [Required]
            [Display(Name = "Username")]
            public string Username { get; set; } = string.Empty;

            [Required]
            [Display(Name = "Password")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;
        }

        public class OUConfigurationInput
        {
            [Required]
            [Display(Name = "Retired Computers OU")]
            public string RetiredComputersOU { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            // Load existing service account
            var existingAccount = await _serviceAccountService.GetActiveServiceAccountAsync();
            HasExistingAccount = existingAccount != null;

            if (existingAccount != null)
            {
                ServiceAccount.Domain = existingAccount.Domain;
                ServiceAccount.Username = existingAccount.Username;
            }

            // Load current retired computers OU
            CurrentRetiredOU = await _ouService.GetRetiredComputersOUAsync();
            if (!string.IsNullOrEmpty(CurrentRetiredOU))
            {
                OUConfig.RetiredComputersOU = CurrentRetiredOU;
            }
        }

        public async Task<IActionResult> OnPostTestSaveAsync()
        {
            StatusMessage = "TEST SAVE METHOD CALLED!";
            return Page();
        }

        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnPostDirectDatabaseTestAsync()
        {
            try
            {
                // Reload data first
                await LoadDataAsync();

                // Direct database test
                var testAccount = new ServiceAccount
                {
                    Domain = "TEST.lan",
                    Username = "testuser",
                    EncryptedPassword = "testpassword",
                    LastUpdated = DateTime.Now,
                    UpdatedBy = "TEST",
                    IsActive = true
                };

                var context = HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                context.ServiceAccounts.Add(testAccount);
                var result = await context.SaveChangesAsync();

                StatusMessage = $"Direct database test: {result} rows saved. Check database for 'testuser' entry.";

                // Also test if we can read it back
                var savedAccount = await context.ServiceAccounts
                    .Where(sa => sa.Username == "testuser")
                    .FirstOrDefaultAsync();

                if (savedAccount != null)
                {
                    StatusMessage += " - Entry confirmed in database!";
                }
                else
                {
                    StatusMessage += " - WARNING: Could not read back from database!";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Direct database test failed: {ex.Message}";
                _logger.LogError(ex, "Direct database test failed");
            }

            return Page();
        }
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnPostSaveServiceAccountAsync()
        {
            // Reload all data first
            await LoadDataAsync();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                var username = User.Identity?.Name ?? "Unknown";
                var success = await _serviceAccountService.SaveServiceAccountAsync(
                    ServiceAccount.Domain,
                    ServiceAccount.Username,
                    ServiceAccount.Password,
                    username);

                if (success)
                {
                    StatusMessage = "Service account credentials saved successfully!";
                    HasExistingAccount = true;
                    // Reload data to show updated state
                    await LoadDataAsync();
                }
                else
                {
                    StatusMessage = "Error saving service account credentials.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving service account");
                StatusMessage = "An error occurred while saving credentials.";
            }

            ServiceAccount.Password = string.Empty;
            return Page();
        }

        public async Task<IActionResult> OnPostTestServiceAccountAsync()
        {
            // Reload all data first
            await LoadDataAsync();

            try
            {
                var success = await _serviceAccountService.TestServiceAccountAsync(
                    ServiceAccount.Domain,
                    ServiceAccount.Username,
                    ServiceAccount.Password);

                StatusMessage = success
                    ? "Connection test successful! Credentials are working."
                    : "Connection test failed. Please check your credentials.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing service account");
                StatusMessage = "Connection test failed with error.";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSaveOUConfigAsync()
        {
            // Reload all data first
            await LoadDataAsync();

            if (string.IsNullOrWhiteSpace(OUConfig.RetiredComputersOU))
            {
                StatusMessage = "Please select a retired computers OU.";
                return Page();
            }

            try
            {
                var username = User.Identity?.Name ?? "Unknown";
                var success = await _ouService.SaveRetiredComputersOUAsync(OUConfig.RetiredComputersOU, username);

                if (success)
                {
                    StatusMessage = "OU configuration saved successfully!";
                    // Reload data to show updated state
                    await LoadDataAsync();
                }
                else
                {
                    StatusMessage = "Error saving OU configuration.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving OU configuration");
                StatusMessage = "An error occurred while saving OU configuration.";
            }

            return Page();
        }
    }
}