// Services/ActiveDirectoryService.cs
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;

namespace PCGroupCloningApp.Services
{
    public class ActiveDirectoryService : IActiveDirectoryService
    {
        private readonly string _domain;
        private readonly IServiceAccountService _serviceAccountService;
        private readonly ILogger<ActiveDirectoryService> _logger;
        private readonly List<string> _systemGroups = new() { "Domain Users", "Domain Computers" };

        public ActiveDirectoryService(IConfiguration configuration, IServiceAccountService serviceAccountService, ILogger<ActiveDirectoryService> logger)
        {
            _domain = configuration["ActiveDirectory:Domain"] ?? "IBK.lan";
            _serviceAccountService = serviceAccountService;
            _logger = logger;
        }

        private async Task<DirectoryEntry> GetDirectoryEntryAsync()
        {
            var ldapPath = $"LDAP://{_domain}";

            var credentials = await _serviceAccountService.GetCredentialsAsync();

            if (credentials.HasValue)
            {
                return new DirectoryEntry(ldapPath, credentials.Value.Username, credentials.Value.Password, AuthenticationTypes.Secure);
            }
            else
            {
                // Fallback til Windows Authentication
                return new DirectoryEntry(ldapPath);
            }
        }

        public async Task<List<string>> SearchComputersAsync(string searchTerm)
        {
            var computers = new List<string>();

            try
            {
                using var entry = await GetDirectoryEntryAsync();
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = $"(&(objectClass=computer)(name=*{searchTerm}*))",
                    PropertiesToLoad = { "name" }
                };

                var results = await Task.Run(() => searcher.FindAll());

                foreach (SearchResult result in results)
                {
                    var name = result.Properties["name"][0]?.ToString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        computers.Add(name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching computers with term: {SearchTerm}", searchTerm);
            }

            return computers.OrderBy(c => c).ToList();
        }

        public async Task<List<string>> GetComputerGroupsAsync(string computerName)
        {
            var groups = new List<string>();

            try
            {
                using var entry = await GetDirectoryEntryAsync();
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = $"(&(objectClass=computer)(name={computerName}))",
                    PropertiesToLoad = { "memberOf" }
                };

                var result = await Task.Run(() => searcher.FindOne());

                if (result?.Properties["memberOf"] != null)
                {
                    foreach (string groupDN in result.Properties["memberOf"])
                    {
                        // Udtræk gruppe navn fra DN
                        var groupName = ExtractCNFromDN(groupDN);
                        if (!string.IsNullOrEmpty(groupName))
                        {
                            groups.Add(groupName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting groups for computer: {ComputerName}", computerName);
            }

            return groups.OrderBy(g => g).ToList();
        }

        public async Task<List<string>> GetComputerGroupsDetailedAsync(string computerName)
        {
            var groups = new List<string>();

            try
            {
                using var entry = await GetDirectoryEntryAsync();
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = $"(&(objectClass=computer)(name={computerName}))",
                    PropertiesToLoad = { "memberOf" }
                };

                var result = await Task.Run(() => searcher.FindOne());

                if (result?.Properties["memberOf"] != null)
                {
                    foreach (string groupDN in result.Properties["memberOf"])
                    {
                        var groupName = ExtractCNFromDN(groupDN);
                        if (!string.IsNullOrEmpty(groupName))
                        {
                            groups.Add(groupName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detailed groups for computer: {ComputerName}", computerName);
            }

            return groups.OrderBy(g => g).ToList();
        }

        public async Task<List<string>> SearchGroupsAsync(string searchTerm)
        {
            var groups = new List<string>();

            try
            {
                using var entry = await GetDirectoryEntryAsync();
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = $"(&(objectClass=group)(name=*{searchTerm}*))",
                    PropertiesToLoad = { "name" }
                };

                var results = await Task.Run(() => searcher.FindAll());

                foreach (SearchResult result in results)
                {
                    var name = result.Properties["name"][0]?.ToString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        groups.Add(name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching groups with term: {SearchTerm}", searchTerm);
            }

            return groups.OrderBy(g => g).ToList();
        }

        public async Task<bool> AddComputerToGroupAsync(string computerName, string groupName)
        {
            try
            {
                var credentials = await _serviceAccountService.GetCredentialsAsync();
                using var context = credentials.HasValue
                    ? new PrincipalContext(ContextType.Domain, _domain, credentials.Value.Username, credentials.Value.Password)
                    : new PrincipalContext(ContextType.Domain, _domain);

                var computer = ComputerPrincipal.FindByIdentity(context, computerName);
                var group = GroupPrincipal.FindByIdentity(context, groupName);

                if (computer == null)
                {
                    _logger.LogWarning("Computer {ComputerName} not found", computerName);
                    return false;
                }

                if (group == null)
                {
                    _logger.LogWarning("Group {GroupName} not found", groupName);
                    return false;
                }

                // Check if computer is already in the group
                if (group.Members.Contains(computer))
                {
                    _logger.LogInformation("Computer {ComputerName} is already a member of group {GroupName} - skipping",
                        computerName, groupName);
                    return true; // Return true instead of false - this is not an error!
                }

                // Add computer to group
                group.Members.Add(computer);
                await Task.Run(() => group.Save());

                _logger.LogInformation("Successfully added computer {ComputerName} to group {GroupName}",
                    computerName, groupName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding computer {Computer} to group {Group}. Error: {ErrorMessage}",
                    computerName, groupName, ex.Message);
                return false;
            }
        }

        public async Task<bool> RemoveComputerFromGroupAsync(string computerName, string groupName)
        {
            try
            {
                var credentials = await _serviceAccountService.GetCredentialsAsync();
                using var context = credentials.HasValue
                    ? new PrincipalContext(ContextType.Domain, _domain, credentials.Value.Username, credentials.Value.Password)
                    : new PrincipalContext(ContextType.Domain, _domain);

                var computer = ComputerPrincipal.FindByIdentity(context, computerName);
                var group = GroupPrincipal.FindByIdentity(context, groupName);

                if (computer == null)
                {
                    _logger.LogWarning("Computer {ComputerName} not found - cannot remove from group {GroupName}",
                        computerName, groupName);
                    return false;
                }

                if (group == null)
                {
                    _logger.LogWarning("Group {GroupName} not found - cannot remove computer {ComputerName}",
                        groupName, computerName);
                    return false;
                }

                if (!group.Members.Contains(computer))
                {
                    _logger.LogInformation("Computer {ComputerName} is not a member of group {GroupName} - already removed",
                        computerName, groupName);
                    return true; // Not an error - already not in group
                }

                group.Members.Remove(computer);
                await Task.Run(() => group.Save());

                _logger.LogInformation("Successfully removed computer {ComputerName} from group {GroupName}",
                    computerName, groupName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing computer {Computer} from group {Group}. Error: {ErrorMessage}",
                    computerName, groupName, ex.Message);
                return false;
            }
        }

        public async Task<bool> RemoveComputerFromMultipleGroupsAsync(string computerName, List<string> groupNames)
        {
            var successCount = 0;
            var totalGroups = groupNames.Count;

            _logger.LogInformation("Removing computer {ComputerName} from {GroupCount} groups",
                computerName, totalGroups);

            foreach (var groupName in groupNames)
            {
                var success = await RemoveComputerFromGroupAsync(computerName, groupName);
                if (success)
                {
                    successCount++;
                    _logger.LogInformation("Successfully removed {ComputerName} from group {GroupName} ({Current}/{Total})",
                        computerName, groupName, successCount, totalGroups);
                }
                else
                {
                    _logger.LogError("Failed to remove {ComputerName} from group {GroupName} - STOPPING OPERATION",
                        computerName, groupName);
                    return false; // Stop ved første fejl
                }
            }

            _logger.LogInformation("Successfully removed computer {ComputerName} from all {GroupCount} groups",
                computerName, successCount);
            return true;
        }

        public async Task<bool> MoveComputerToOUAsync(string computerName, string targetOU)
        {
            try
            {
                // Brug PrincipalContext approach i stedet for DirectoryEntry
                var credentials = await _serviceAccountService.GetCredentialsAsync();
                using var context = credentials.HasValue
                    ? new PrincipalContext(ContextType.Domain, _domain, credentials.Value.Username, credentials.Value.Password)
                    : new PrincipalContext(ContextType.Domain, _domain);

                // Find computeren
                var computer = ComputerPrincipal.FindByIdentity(context, computerName);
                if (computer == null)
                {
                    _logger.LogWarning("Computer {ComputerName} not found", computerName);
                    return false;
                }

                // Log current location
                _logger.LogInformation("Current computer location: {CurrentDN}", computer.DistinguishedName);
                _logger.LogInformation("Target OU: {TargetOU}", targetOU);

                // Get DirectoryEntry for the computer
                var computerEntry = computer.GetUnderlyingObject() as DirectoryEntry;
                if (computerEntry == null)
                {
                    _logger.LogError("Could not get DirectoryEntry for computer {ComputerName}", computerName);
                    return false;
                }

                // Build correct LDAP path for target OU
                var targetLdapPath = $"LDAP://{_domain}/{targetOU}";
                _logger.LogInformation("Moving to LDAP path: {TargetPath}", targetLdapPath);

                // Create target OU entry
                using var targetEntry = credentials.HasValue
                    ? new DirectoryEntry(targetLdapPath, credentials.Value.Username, credentials.Value.Password, AuthenticationTypes.Secure)
                    : new DirectoryEntry(targetLdapPath);

                // Perform the move
                await Task.Run(() => computerEntry.MoveTo(targetEntry));

                _logger.LogInformation("Successfully moved computer {ComputerName} to {TargetOU}", computerName, targetOU);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving computer {Computer} to OU {OU}. Error: {ErrorMessage}",
                    computerName, targetOU, ex.Message);
                return false;
            }
        }

        public async Task<string> GetComputerOUAsync(string computerName)
        {
            try
            {
                using var entry = await GetDirectoryEntryAsync();
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = $"(&(objectClass=computer)(name={computerName}))",
                    PropertiesToLoad = { "distinguishedName" }
                };

                var result = await Task.Run(() => searcher.FindOne());

                if (result?.Properties["distinguishedName"][0] is string dn)
                {
                    // Udtræk OU fra DN
                    var ouStart = dn.IndexOf(",OU=");
                    if (ouStart > 0)
                    {
                        return dn.Substring(ouStart + 1);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting OU for computer: {ComputerName}", computerName);
            }

            return string.Empty;
        }

        public async Task<(string OU, string OUDescription, string ComputerDescription)> GetComputerDetailsAsync(string computerName)
        {
            try
            {
                using var entry = await GetDirectoryEntryAsync();
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = $"(&(objectClass=computer)(name={computerName}))",
                    PropertiesToLoad = { "distinguishedName", "description" }
                };

                var result = await Task.Run(() => searcher.FindOne());

                if (result != null)
                {
                    // Get computer description
                    string computerDescription = string.Empty;
                    if (result.Properties["description"].Count > 0)
                    {
                        computerDescription = result.Properties["description"][0]?.ToString() ?? string.Empty;
                    }

                    // Get OU path
                    if (result.Properties["distinguishedName"][0] is string dn)
                    {
                        var ouStart = dn.IndexOf(",OU=");
                        if (ouStart > 0)
                        {
                            var ouPath = dn.Substring(ouStart + 1);

                            // Hent OU description
                            var ouDescription = await GetOUDescriptionAsync(ouPath);

                            return (ouPath, ouDescription, computerDescription);
                        }
                    }

                    return (string.Empty, string.Empty, computerDescription);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting details for computer: {ComputerName}", computerName);
            }

            return (string.Empty, string.Empty, string.Empty);
        }

        private async Task<string> GetOUDescriptionAsync(string ouPath)
        {
            try
            {
                var ldapPath = $"LDAP://{_domain}/{ouPath}";

                var credentials = await _serviceAccountService.GetCredentialsAsync();

                using var ouEntry = credentials.HasValue
                    ? new DirectoryEntry(ldapPath, credentials.Value.Username, credentials.Value.Password, AuthenticationTypes.Secure)
                    : new DirectoryEntry(ldapPath);

                if (ouEntry.Properties["description"].Count > 0)
                {
                    return ouEntry.Properties["description"][0]?.ToString() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting description for OU: {OU}", ouPath);
            }

            return string.Empty;
        }

        private static string ExtractCNFromDN(string distinguishedName)
        {
            if (distinguishedName.StartsWith("CN="))
            {
                var endIndex = distinguishedName.IndexOf(',');
                if (endIndex > 3)
                {
                    return distinguishedName.Substring(3, endIndex - 3);
                }
            }
            return string.Empty;
        }
    }
}