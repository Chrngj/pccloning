// Pages/Admin/Management.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PCGroupCloningApp.Services;
using System.ComponentModel.DataAnnotations;

namespace PCGroupCloningApp.Pages.Admin
{
    public class ManagementModel : PageModel
    {
        private readonly IServiceAccountService _serviceAccountService;
        private readonly IOUService _ouService;
        private readonly ILogger<ManagementModel> _logger;

        public ManagementModel(
            IServiceAccountService serviceAccountService,
            IOUService ouService,
            ILogger<ManagementModel> logger)
        {
            _serviceAccountService = serviceAccountService;
            _ouService = ouService;
            _logger = logger;
        }

        // Service Account Properties
        [BindProperty]
        public ServiceAccountInput ServiceAccount { get; set; } = new();
        public bool HasExistingServiceAccount { get; set; }
        public string? ServiceAccountStatus { get; set; }

        // OU Configuration Properties  
        [BindProperty]
        public OUConfigurationInput OUConfig { get; set; } = new();
        public string? CurrentRetiredOU { get; set; }
        public string? OUConfigStatus { get; set; }

        // Input Models
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

        // Page Load
        public async Task OnGetAsync()
        {
            await LoadServiceAccountDataAsync();
            await LoadOUConfigurationDataAsync();
        }

        // SERVICE ACCOUNT HANDLERS
        public async Task<IActionResult> OnPostSaveServiceAccountAsync()
        {
            _logger.LogInformation("SaveServiceAccount called - Username: {Username}", ServiceAccount.Username);

            // Only validate ServiceAccount properties
            if (string.IsNullOrWhiteSpace(ServiceAccount.Username) ||
                string.IsNullOrWhiteSpace(ServiceAccount.Password))
            {
                ServiceAccountStatus = "Username and password are required.";
                await LoadAllDataAsync();
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
                    ServiceAccountStatus = "✅ Service account credentials saved successfully!";
                    HasExistingServiceAccount = true;
                    _logger.LogInformation("Service account saved successfully");
                }
                else
                {
                    ServiceAccountStatus = "❌ Error saving service account credentials.";
                    _logger.LogWarning("Failed to save service account");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception saving service account");
                ServiceAccountStatus = "❌ An error occurred while saving credentials.";
            }

            // Clear password and reload data
            ServiceAccount.Password = string.Empty;
            await LoadAllDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostTestServiceAccountAsync()
        {
            _logger.LogInformation("TestServiceAccount called - Username: {Username}", ServiceAccount.Username);

            if (string.IsNullOrWhiteSpace(ServiceAccount.Username) ||
                string.IsNullOrWhiteSpace(ServiceAccount.Password))
            {
                ServiceAccountStatus = "Username and password are required for testing.";
                await LoadAllDataAsync();
                return Page();
            }

            try
            {
                var success = await _serviceAccountService.TestServiceAccountAsync(
                    ServiceAccount.Domain,
                    ServiceAccount.Username,
                    ServiceAccount.Password);

                ServiceAccountStatus = success
                    ? "✅ Connection test successful! Credentials are working."
                    : "❌ Connection test failed. Please check your credentials.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception testing service account");
                ServiceAccountStatus = "❌ Connection test failed with error.";
            }

            await LoadAllDataAsync();
            return Page();
        }

        // OU CONFIGURATION HANDLERS
        public async Task<IActionResult> OnPostSaveOUConfigAsync()
        {
            _logger.LogInformation("SaveOUConfig called - OU: {OU}", OUConfig.RetiredComputersOU);

            if (string.IsNullOrWhiteSpace(OUConfig.RetiredComputersOU))
            {
                OUConfigStatus = "Please select a retired computers OU.";
                await LoadAllDataAsync();
                return Page();
            }

            try
            {
                var username = User.Identity?.Name ?? "Unknown";
                var success = await _ouService.SaveRetiredComputersOUAsync(
                    OUConfig.RetiredComputersOU,
                    username);

                if (success)
                {
                    OUConfigStatus = "✅ OU configuration saved successfully!";
                    _logger.LogInformation("OU configuration saved successfully");
                }
                else
                {
                    OUConfigStatus = "❌ Error saving OU configuration.";
                    _logger.LogWarning("Failed to save OU configuration");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception saving OU configuration");
                OUConfigStatus = "❌ An error occurred while saving OU configuration.";
            }

            await LoadAllDataAsync();
            return Page();
        }

        // DATA LOADING METHODS
        private async Task LoadServiceAccountDataAsync()
        {
            try
            {
                var existingAccount = await _serviceAccountService.GetActiveServiceAccountAsync();
                HasExistingServiceAccount = existingAccount != null;

                if (existingAccount != null)
                {
                    ServiceAccount.Domain = existingAccount.Domain;
                    ServiceAccount.Username = existingAccount.Username;
                    // Never load password back to form
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading service account data");
                ServiceAccountStatus = "Error loading service account information.";
            }
        }

        private async Task LoadOUConfigurationDataAsync()
        {
            try
            {
                CurrentRetiredOU = await _ouService.GetRetiredComputersOUAsync();
                if (!string.IsNullOrEmpty(CurrentRetiredOU))
                {
                    OUConfig.RetiredComputersOU = CurrentRetiredOU;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading OU configuration data");
                OUConfigStatus = "Error loading OU configuration.";
            }
        }

        private async Task LoadAllDataAsync()
        {
            await LoadServiceAccountDataAsync();
            await LoadOUConfigurationDataAsync();
        }
    }
}