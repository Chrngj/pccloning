// Services/IOUService.cs
namespace PCGroupCloningApp.Services
{
    public interface IOUService
    {
        Task<List<string>> SearchOUsAsync(string searchTerm);
        Task<string?> GetRetiredComputersOUAsync();
        Task<bool> SaveRetiredComputersOUAsync(string ouPath, string updatedBy);
    }
}