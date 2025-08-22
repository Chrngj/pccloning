// Services/ServiceAccountService.cs
using Microsoft.EntityFrameworkCore;
using PCGroupCloningApp.Data;
using PCGroupCloningApp.Models;
using System.DirectoryServices;

namespace PCGroupCloningApp.Services
{
    public class ServiceAccountService : IServiceAccountService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<ServiceAccountService> _logger;

        public ServiceAccountService(ApplicationDbContext context, IEncryptionService encryptionService, ILogger<ServiceAccountService> logger)
        {
            _context = context;
            _encryptionService = encryptionService;
            _logger = logger;
        }

        public async Task<ServiceAccount?> GetActiveServiceAccountAsync()
        {
            return await _context.ServiceAccounts
                .Where(sa => sa.IsActive)
                .OrderByDescending(sa => sa.LastUpdated)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> SaveServiceAccountAsync(string domain, string username, string password, string updatedBy)
        {
            try
            {
                // Deactivate existing accounts
                var existingAccounts = await _context.ServiceAccounts.Where(sa => sa.IsActive).ToListAsync();
                foreach (var account in existingAccounts)
                {
                    account.IsActive = false;
                }

                // Create new service account entry
                var serviceAccount = new ServiceAccount
                {
                    Domain = domain,
                    Username = username,
                    EncryptedPassword = _encryptionService.Encrypt(password),
                    LastUpdated = DateTime.Now,
                    UpdatedBy = updatedBy,
                    IsActive = true
                };

                _context.ServiceAccounts.Add(serviceAccount);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving service account");
                return false;
            }
        }

        public async Task<bool> TestServiceAccountAsync(string domain, string username, string password)
        {
            try
            {
                var ldapPath = $"LDAP://{domain}";
                using var entry = new DirectoryEntry(ldapPath, username, password, AuthenticationTypes.Secure);

                // Try to access the RootDSE to test connection
                var rootDse = entry.NativeObject;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Service account test failed for user: {Username}", username);
                return false;
            }
        }

        public async Task<(string Username, string Password)?> GetCredentialsAsync()
        {
            var serviceAccount = await GetActiveServiceAccountAsync();
            if (serviceAccount == null)
                return null;

            var decryptedPassword = _encryptionService.Decrypt(serviceAccount.EncryptedPassword);
            var fullUsername = $"{serviceAccount.Domain}\\{serviceAccount.Username}";

            return (fullUsername, decryptedPassword);
        }
    }
}