// Pages/Admin/ServiceAccount.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PCGroupCloningApp.Services;
using System.ComponentModel.DataAnnotations;

namespace PCGroupCloningApp.Pages.Admin
{
    public class ServiceAccountModel : PageModel
    {
        private readonly IServiceAccountService _serviceAccountService;
        private readonly ILogger<ServiceAccountModel> _logger;

        public ServiceAccountModel(IServiceAccountService serviceAccountService, ILogger<ServiceAccountModel> logger)
        {
            _serviceAccountService = serviceAccountService;
            _logger = logger;
        }

        [BindProperty]
        public ServiceAccountInput Input { get; set; } = new();

        public string? StatusMessage { get; set; }
        public bool HasExistingAccount { get; set; }

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

        public async Task OnGetAsync()
        {
            var existingAccount = await _serviceAccountService.GetActiveServiceAccountAsync();
            HasExistingAccount = existingAccount != null;

            if (existingAccount != null)
            {
                Input.Domain = existingAccount.Domain;
                Input.Username = existingAccount.Username;
            }
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                var username = User.Identity?.Name ?? "Unknown";
                var success = await _serviceAccountService.SaveServiceAccountAsync(
                    Input.Domain,
                    Input.Username,
                    Input.Password,
                    username);

                if (success)
                {
                    StatusMessage = "Service account credentials saved successfully!";
                    HasExistingAccount = true;
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

            // Clear password for security
            Input.Password = string.Empty;
            return Page();
        }

        public async Task<IActionResult> OnPostTestAsync()
        {
            if (string.IsNullOrEmpty(Input.Username) || string.IsNullOrEmpty(Input.Password))
            {
                StatusMessage = "Please enter both username and password to test.";
                return Page();
            }

            try
            {
                var success = await _serviceAccountService.TestServiceAccountAsync(
                    Input.Domain,
                    Input.Username,
                    Input.Password);

                StatusMessage = success
                    ? " Connection test successful! Credentials are working."
                    : " Connection test failed. Please check your credentials.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing service account");
                StatusMessage = " Connection test failed with error.";
            }

            return Page();
        }
    }
}