// Services/IServiceAccountService.cs
using PCGroupCloningApp.Models;

namespace PCGroupCloningApp.Services
{
    public interface IServiceAccountService
    {
        Task<ServiceAccount?> GetActiveServiceAccountAsync();
        Task<bool> SaveServiceAccountAsync(string domain, string username, string password, string updatedBy);
        Task<bool> TestServiceAccountAsync(string domain, string username, string password);
        Task<(string Username, string Password)?> GetCredentialsAsync();
    }
}