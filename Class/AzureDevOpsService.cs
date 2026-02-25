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
    }

}
