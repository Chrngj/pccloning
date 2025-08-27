// Services/IActiveDirectoryService.cs
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;

namespace PCGroupCloningApp.Services
{
    public interface IActiveDirectoryService
    {
        Task<List<string>> SearchComputersAsync(string searchTerm);
        Task<List<string>> GetComputerGroupsAsync(string computerName);
        Task<List<string>> SearchGroupsAsync(string searchTerm);
        Task<bool> AddComputerToGroupAsync(string computerName, string groupName);
        Task<bool> RemoveComputerFromGroupAsync(string computerName, string groupName);
        Task<bool> MoveComputerToOUAsync(string computerName, string targetOU);
        Task<string> GetComputerOUAsync(string computerName);
        Task<List<string>> GetComputerGroupsDetailedAsync(string computerName);
        Task<bool> RemoveComputerFromMultipleGroupsAsync(string computerName, List<string> groupNames);
    }
}
