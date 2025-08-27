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
            try
            {
                _logger.LogInformation("Getting active service account");
                var result = await _context.ServiceAccounts
                    .Where(sa => sa.IsActive)
                    .OrderByDescending(sa => sa.LastUpdated)
                    .FirstOrDefaultAsync();
                _logger.LogInformation("Found active service account: {Found}", result != null);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active service account");
                return null;
            }
        }

        public async Task<bool> SaveServiceAccountAsync(string domain, string username, string password, string updatedBy)
        {
            try
            {
                _logger.LogInformation("SaveServiceAccountAsync called - Domain: {Domain}, Username: {Username}, UpdatedBy: {UpdatedBy}",
                    domain, username, updatedBy);

                // Test encryption first
                _logger.LogInformation("Testing encryption...");
                var encryptedPassword = _encryptionService.Encrypt(password);
                _logger.LogInformation("Encryption successful, encrypted length: {Length}", encryptedPassword?.Length ?? 0);

                // Deactivate existing accounts
                _logger.LogInformation("Looking for existing active accounts...");
                var existingAccounts = await _context.ServiceAccounts.Where(sa => sa.IsActive).ToListAsync();
                _logger.LogInformation("Found {Count} existing active accounts", existingAccounts.Count);

                foreach (var account in existingAccounts)
                {
                    account.IsActive = false;
                    _logger.LogInformation("Deactivated account: {Username}", account.Username);
                }

                // Create new service account entry
                _logger.LogInformation("Creating new service account object...");
                var serviceAccount = new ServiceAccount
                {
                    Domain = domain,
                    Username = username,
                    EncryptedPassword = encryptedPassword,
                    LastUpdated = DateTime.Now,
                    UpdatedBy = updatedBy,
                    IsActive = true
                };

                _logger.LogInformation("Adding new service account to context...");
                _context.ServiceAccounts.Add(serviceAccount);

                _logger.LogInformation("Calling SaveChangesAsync...");
                var result = await _context.SaveChangesAsync();
                _logger.LogInformation("SaveChanges returned: {Result} rows affected", result);

                if (result > 0)
                {
                    _logger.LogInformation("Service account saved successfully");
                    return true;
                }
                else
                {
                    _logger.LogWarning("SaveChanges returned 0 rows affected");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving service account - Domain: {Domain}, Username: {Username}", domain, username);
                return false;
            }
        }

        public async Task<bool> TestServiceAccountAsync(string domain, string username, string password)
        {
            try
            {
                _logger.LogInformation("Testing service account - Domain: {Domain}, Username: {Username}", domain, username);
                var ldapPath = $"LDAP://{domain}";
                using var entry = new DirectoryEntry(ldapPath, username, password, AuthenticationTypes.Secure);

                // Try to access the RootDSE to test connection
                var rootDse = entry.NativeObject;
                _logger.LogInformation("Service account test successful for user: {Username}", username);
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
            try
            {
                _logger.LogInformation("Getting credentials...");
                var serviceAccount = await GetActiveServiceAccountAsync();
                if (serviceAccount == null)
                {
                    _logger.LogInformation("No active service account found");
                    return null;
                }

                _logger.LogInformation("Decrypting password for account: {Username}", serviceAccount.Username);
                var decryptedPassword = _encryptionService.Decrypt(serviceAccount.EncryptedPassword);
                var fullUsername = $"{serviceAccount.Domain}\\{serviceAccount.Username}";

                _logger.LogInformation("Credentials retrieved successfully for: {Username}", fullUsername);
                return (fullUsername, decryptedPassword);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting credentials");
                return null;
            }
        }
    }
}