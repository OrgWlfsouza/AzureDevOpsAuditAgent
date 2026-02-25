using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;

namespace AzureDevOpsAuditAgent.Class
{
    public class AzureDevOpsService
    {
        private readonly HttpClient _httpClient;
        private readonly string _organization;

        public AzureDevOpsService(IConfiguration config, HttpClient httpClient)
        {
            _httpClient = httpClient;
            _organization = config["AzureDevOps:Organization"];
            var pat = config["AzureDevOps:PAT"];
            var byteArray = System.Text.Encoding.ASCII.GetBytes($":{pat}");
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        public async Task<int> GetProjectCountAsync()
        {
            var url = $"https://dev.azure.com/{_organization}/_apis/projects?api-version=7.0";
            var response = await _httpClient.GetStringAsync(url);
            var json = JObject.Parse(response);
            return json["value"].Count();
        }

        public async Task<int> GetUserCountAsync()
        {
            var url = $"https://vsaex.dev.azure.com/{_organization}/_apis/userentitlements?api-version=7.0-preview.3";
            var response = await _httpClient.GetStringAsync(url);
            var json = JObject.Parse(response);
            return json["members"].Count();
        }

        public async Task<string> GetUserLicenseAsync(string userPrincipalName)
        {
            var url = $"https://vsaex.dev.azure.com/{_organization}/_apis/userentitlements?api-version=7.0-preview.3";
            var response = await _httpClient.GetStringAsync(url);
            var json = JObject.Parse(response);

            var user = json["members"]
                .FirstOrDefault(u => u["user"]["principalName"].ToString() == userPrincipalName);

            return user?["accessLevel"]["accountLicenseType"]?.ToString() ?? "Não encontrado";
        }
        public async Task<List<string>> GetProjectAdministratorsAsync(string projectId)
        {
            // Primeiro, obter o namespace de segurança
            var nsUrl = $"https://dev.azure.com/{_organization}/_apis/securitynamespaces?api-version=7.0";
            var nsResponse = await _httpClient.GetStringAsync(nsUrl);
            var nsJson = JObject.Parse(nsResponse);

            var projectNamespace = nsJson["value"]
                .FirstOrDefault(n => n["name"].ToString() == "Project");

            if (projectNamespace == null)
                return new List<string>();

            var namespaceId = projectNamespace["namespaceId"].ToString();

            // Agora, obter as permissões dos usuários no projeto
            var aceUrl = $"https://dev.azure.com/{_organization}/_apis/accesscontrolentries/{namespaceId}?api-version=7.0";
            var aceResponse = await _httpClient.GetStringAsync(aceUrl);
            var aceJson = JObject.Parse(aceResponse);

            var admins = new List<string>();

            foreach (var entry in aceJson["value"])
            {
                var descriptor = entry["descriptor"]?.ToString();
                var allowPermissions = entry["allow"]?.ToString();

                // O valor da permissão de Project Administrator é específico (bitmask).
                // Exemplo: 8192 pode representar "Administer project".
                if (allowPermissions != null && allowPermissions.Contains("8192"))
                {
                    admins.Add(descriptor);
                }
            }

            return admins;
        }

        public async Task<string> ResolveDescriptorAsync(string descriptor)
        {
            var url = $"https://vssps.dev.azure.com/{_organization}/_apis/graph/descriptors/{descriptor}?api-version=7.0-preview.1";
            var response = await _httpClient.GetStringAsync(url);
            var json = JObject.Parse(response);

            return json["value"]?[0]?["principalName"]?.ToString()
                   ?? json["value"]?[0]?["displayName"]?.ToString()
                   ?? descriptor;
        }

        public async Task<List<string>> GetProjectAdministratorsResolvedAsync(string projectId)
        {
            var admins = await GetProjectAdministratorsAsync(projectId);
            var resolved = new List<string>();

            foreach (var descriptor in admins)
            {
                var name = await ResolveDescriptorAsync(descriptor);
                resolved.Add(name);
            }

            return resolved;
        }
    }

}
