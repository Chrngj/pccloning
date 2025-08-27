// Services/OUService.cs
using Microsoft.EntityFrameworkCore;
using PCGroupCloningApp.Data;
using PCGroupCloningApp.Models;
using System.DirectoryServices;

namespace PCGroupCloningApp.Services
{
    public class OUService : IOUService
    {
        private readonly IActiveDirectoryService _adService;
        private readonly ApplicationDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly IServiceAccountService _serviceAccountService;
        private readonly string _domain;
        private readonly ILogger<OUService> _logger;

        public OUService(
            IActiveDirectoryService adService,
            ApplicationDbContext context,
            IEncryptionService encryptionService,
            IServiceAccountService serviceAccountService,
            IConfiguration configuration,
            ILogger<OUService> logger)
        {
            _adService = adService;
            _context = context;
            _encryptionService = encryptionService;
            _serviceAccountService = serviceAccountService;
            _domain = configuration["ActiveDirectory:Domain"] ?? "IBK.lan";
            _logger = logger;
        }

        public async Task<List<string>> SearchOUsAsync(string searchTerm)
        {
            var ous = new List<string>();

            try
            {
                var credentials = await _serviceAccountService.GetCredentialsAsync();
                var ldapPath = $"LDAP://{_domain}";

                using var entry = credentials.HasValue
                    ? new DirectoryEntry(ldapPath, credentials.Value.Username, credentials.Value.Password, AuthenticationTypes.Secure)
                    : new DirectoryEntry(ldapPath);

                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = $"(&(objectClass=organizationalUnit)(name=*{searchTerm}*))",
                    PropertiesToLoad = { "distinguishedName", "name" }
                };

                var results = await Task.Run(() => searcher.FindAll());

                foreach (SearchResult result in results)
                {
                    var dn = result.Properties["distinguishedName"][0]?.ToString();
                    if (!string.IsNullOrEmpty(dn))
                    {
                        ous.Add(dn);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching OUs with term: {SearchTerm}", searchTerm);
            }

            return ous.OrderBy(ou => ou).ToList();
        }

        public async Task<string?> GetRetiredComputersOUAsync()
        {
            try
            {
                var config = await _context.OUConfigurations
                    .Where(c => c.IsActive)
                    .OrderByDescending(c => c.LastUpdated)
                    .FirstOrDefaultAsync();

                return config?.RetiredComputersOU;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting retired computers OU");
                return null;
            }
        }

        public async Task<bool> SaveRetiredComputersOUAsync(string ouPath, string updatedBy)
        {
            try
            {
                // Deactivate existing configurations
                var existingConfigs = await _context.OUConfigurations.Where(c => c.IsActive).ToListAsync();
                foreach (var config in existingConfigs)
                {
                    config.IsActive = false;
                }

                // Create new configuration
                var newConfig = new OUConfiguration
                {
                    RetiredComputersOU = ouPath,
                    LastUpdated = DateTime.Now,
                    UpdatedBy = updatedBy,
                    IsActive = true
                };

                _context.OUConfigurations.Add(newConfig);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving retired computers OU");
                return false;
            }
        }
    }
}