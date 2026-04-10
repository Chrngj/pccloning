// Services/ISCCMService.cs
namespace PCGroupCloningApp.Services
{
    public interface ISCCMService
    {
        /// <summary>
        /// Find a device in SCCM by computer name. Returns ResourceID or null if not found.
        /// </summary>
        Task<int?> GetDeviceResourceIdAsync(string computerName);

        /// <summary>
        /// Get all #OSD collections the device is a member of.
        /// </summary>
        Task<List<SCCMCollectionInfo>> GetDeviceOSDCollectionsAsync(int resourceId);

        /// <summary>
        /// Get all available #OSD Role collections.
        /// </summary>
        Task<List<SCCMCollectionInfo>> GetAllOSDRoleCollectionsAsync();

        /// <summary>
        /// Add a device to an SCCM collection via direct membership rule.
        /// </summary>
        Task<bool> AddDeviceToCollectionAsync(int resourceId, string computerName, string collectionId);

        /// <summary>
        /// Remove a device from an SCCM collection via direct membership rule.
        /// </summary>
        Task<bool> RemoveDeviceFromCollectionAsync(int resourceId, string computerName, string collectionId);

        /// <summary>
        /// Delete a device completely from SCCM.
        /// </summary>
        Task<bool> DeleteDeviceAsync(int resourceId);
    }

    public class SCCMCollectionInfo
    {
        public string CollectionID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int MemberCount { get; set; }
    }
}