// Services/SCCMService.cs
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PCGroupCloningApp.Services
{
    public class SCCMService : ISCCMService
    {
        private readonly IServiceAccountService _serviceAccountService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SCCMService> _logger;
        private readonly string _sccmServerUrl;

        // The 5 moveable #OSD Role collections
        private static readonly Dictionary<string, string> OSDRoleCollections = new()
        {
            ["PS30062F"] = "#OSD - Role - Administrativ (ADM)",
            ["PS3006C8"] = "#OSD - Role - Administrativ (ADMUP)",
            ["PS300666"] = "#OSD - Role - Basis PC (BPC)",
            ["PS300643"] = "#OSD - Role - Politiker (POL)",
            ["PS300645"] = "#OSD - Role - Valg (VAL)"
        };

        // All 6 OSD Role collections we check (including No Role assigned)
        private static readonly Dictionary<string, string> AllOSDRoleCollections = new()
        {
            ["PS300660"] = "#OSD - Role - No Role assigned",
            ["PS30062F"] = "#OSD - Role - Administrativ (ADM)",
            ["PS3006C8"] = "#OSD - Role - Administrativ (ADMUP)",
            ["PS300666"] = "#OSD - Role - Basis PC (BPC)",
            ["PS300643"] = "#OSD - Role - Politiker (POL)",
            ["PS300645"] = "#OSD - Role - Valg (VAL)"
        };

        public SCCMService(
            IServiceAccountService serviceAccountService,
            IConfiguration configuration,
            ILogger<SCCMService> logger)
        {
            _serviceAccountService = serviceAccountService;
            _configuration = configuration;
            _logger = logger;
            _sccmServerUrl = configuration["SCCM:ServerUrl"] ?? "https://srvikecm01.ibk.lan/AdminService";
        }

        private async Task<HttpClient> CreateHttpClientAsync()
        {
            var credentials = await _serviceAccountService.GetCredentialsAsync();

            // Create handler that ignores SSL certificate errors (internal server)
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            if (credentials.HasValue)
            {
                // Use service account credentials
                var parts = credentials.Value.Username.Split('\\');
                var domain = parts.Length > 1 ? parts[0] : "";
                var username = parts.Length > 1 ? parts[1] : credentials.Value.Username;

                handler.Credentials = new NetworkCredential(username, credentials.Value.Password, domain);
            }
            else
            {
                handler.UseDefaultCredentials = true;
            }

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return client;
        }

        public async Task<int?> GetDeviceResourceIdAsync(string computerName)
        {
            try
            {
                using var client = await CreateHttpClientAsync();
                var url = $"{_sccmServerUrl}/v1.0/Device?$filter=Name eq '{computerName}'";

                _logger.LogInformation("SCCM: Looking up device {ComputerName}", computerName);
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var values = doc.RootElement.GetProperty("value");
                if (values.GetArrayLength() == 0)
                {
                    _logger.LogInformation("SCCM: Device {ComputerName} not found", computerName);
                    return null;
                }

                var machineId = values[0].GetProperty("MachineId").GetInt32();
                _logger.LogInformation("SCCM: Device {ComputerName} found with ResourceID {ResourceId}", computerName, machineId);
                return machineId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SCCM: Error looking up device {ComputerName}", computerName);
                return null;
            }
        }

        public async Task<List<SCCMCollectionInfo>> GetDeviceOSDCollectionsAsync(int resourceId)
        {
            var osdCollections = new List<SCCMCollectionInfo>();

            try
            {
                using var client = await CreateHttpClientAsync();
                _logger.LogInformation("SCCM: Checking 6 known OSD Role collections for ResourceID {ResourceId}", resourceId);

                // Check only the 6 known OSD Role collections directly
                foreach (var kvp in AllOSDRoleCollections)
                {
                    try
                    {
                        var url = $"{_sccmServerUrl}/wmi/SMS_FullCollectionMembership?$filter=CollectionID eq '{kvp.Key}' and ResourceID eq {resourceId}";
                        var response = await client.GetAsync(url);
                        response.EnsureSuccessStatusCode();

                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        var values = doc.RootElement.GetProperty("value");

                        if (values.GetArrayLength() > 0)
                        {
                            osdCollections.Add(new SCCMCollectionInfo
                            {
                                CollectionID = kvp.Key,
                                Name = kvp.Value,
                                MemberCount = 0
                            });
                            _logger.LogInformation("SCCM: ResourceID {ResourceId} IS member of {Collection}", resourceId, kvp.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "SCCM: Error checking membership for collection {CollectionId}", kvp.Key);
                    }
                }

                _logger.LogInformation("SCCM: Found {Count} OSD Role collections for ResourceID {ResourceId}",
                    osdCollections.Count, resourceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SCCM: Error getting OSD collections for ResourceID {ResourceId}", resourceId);
            }

            return osdCollections;
        }

        public async Task<List<SCCMCollectionInfo>> GetAllOSDRoleCollectionsAsync()
        {
            // Return the known moveable role collections
            return OSDRoleCollections.Select(kvp => new SCCMCollectionInfo
            {
                CollectionID = kvp.Key,
                Name = kvp.Value,
                MemberCount = 0
            }).ToList();
        }

        public async Task<bool> AddDeviceToCollectionAsync(int resourceId, string computerName, string collectionId)
        {
            try
            {
                using var client = await CreateHttpClientAsync();
                var url = $"{_sccmServerUrl}/wmi/SMS_Collection('{collectionId}')/AdminService.AddMembershipRule";

                var body = new
                {
                    collectionRule = new
                    {
                        @odata_type = "#AdminService.SMS_CollectionRuleDirect",
                        RuleName = computerName,
                        ResourceClassName = "SMS_R_System",
                        ResourceID = resourceId
                    }
                };

                // Build JSON manually to handle @odata.type correctly
                var jsonBody = $@"{{
    ""collectionRule"": {{
        ""@odata.type"": ""#AdminService.SMS_CollectionRuleDirect"",
        ""RuleName"": ""{computerName}"",
        ""ResourceClassName"": ""SMS_R_System"",
        ""ResourceID"": {resourceId}
    }}
}}";

                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                _logger.LogInformation("SCCM: Adding {ComputerName} (ResourceID: {ResourceId}) to collection {CollectionId}",
                    computerName, resourceId, collectionId);

                var response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("SCCM: Successfully added {ComputerName} to collection {CollectionId}. Response: {Response}",
                    computerName, collectionId, responseJson);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SCCM: Error adding {ComputerName} to collection {CollectionId}",
                    computerName, collectionId);
                return false;
            }
        }

        public async Task<bool> RemoveDeviceFromCollectionAsync(int resourceId, string computerName, string collectionId)
        {
            try
            {
                using var client = await CreateHttpClientAsync();
                var url = $"{_sccmServerUrl}/wmi/SMS_Collection('{collectionId}')/AdminService.DeleteMembershipRule";

                var jsonBody = $@"{{
    ""collectionRule"": {{
        ""@odata.type"": ""#AdminService.SMS_CollectionRuleDirect"",
        ""RuleName"": ""{computerName}"",
        ""ResourceClassName"": ""SMS_R_System"",
        ""ResourceID"": {resourceId}
    }}
}}";

                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                _logger.LogInformation("SCCM: Removing {ComputerName} (ResourceID: {ResourceId}) from collection {CollectionId}",
                    computerName, resourceId, collectionId);

                var response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("SCCM: Successfully removed {ComputerName} from collection {CollectionId}. Response: {Response}",
                    computerName, collectionId, responseJson);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SCCM: Error removing {ComputerName} from collection {CollectionId}",
                    computerName, collectionId);
                return false;
            }
        }

        public async Task<bool> DeleteDeviceAsync(int resourceId)
        {
            try
            {
                using var client = await CreateHttpClientAsync();
                var url = $"{_sccmServerUrl}/wmi/SMS_R_System({resourceId})";

                _logger.LogInformation("SCCM: Deleting device with ResourceID {ResourceId}", resourceId);

                var response = await client.DeleteAsync(url);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("SCCM: Successfully deleted device with ResourceID {ResourceId}", resourceId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SCCM: Error deleting device with ResourceID {ResourceId}", resourceId);
                return false;
            }
        }
    }
}