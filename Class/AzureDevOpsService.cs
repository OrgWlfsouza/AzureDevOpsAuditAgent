using Microsoft.FeatureManagement;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;

namespace AzureDevOpsAuditAgent.Class
{
    public class AzureDevOpsService
    {
        private readonly HttpClient _httpClient;
        private readonly string _organization;
        private readonly ILogger<AzureDevOpsService>? _logger;
        private readonly IFeatureManager _featureManager;

        public AzureDevOpsService(
            IConfiguration config,
            HttpClient httpClient,
            IFeatureManager featureManager,
            ILogger<AzureDevOpsService>? logger = null)
        {
            _httpClient = httpClient;
            _organization = config["AzureDevOps:Organization"] ?? throw new ArgumentNullException(nameof(config), "AzureDevOps:Organization configuration is required");
            _logger = logger;
            _featureManager = featureManager;
            var pat = config["AzureDevOps:PAT"] ?? throw new ArgumentNullException(nameof(config), "AzureDevOps:PAT configuration is required");
            var byteArray = System.Text.Encoding.ASCII.GetBytes($":{pat}");
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        /// <summary>
        /// Método auxiliar para verificar se uma feature flag está habilitada
        /// </summary>
        /// <param name="featureName">Nome da feature flag</param>
        /// <exception cref="InvalidOperationException">Lançado quando a feature está desabilitada</exception>
        private async Task EnsureFeatureEnabledAsync(string featureName)
        {
            if (!await _featureManager.IsEnabledAsync(featureName))
            {
                _logger?.LogWarning("Tentativa de executar operação com feature '{FeatureName}' desabilitada.", featureName);
                throw new InvalidOperationException(
                    $"Operação bloqueada: A feature '{featureName}' está desabilitada. " +
                    $"Verifique as configurações de Feature Flags no appsettings.json.");
            }
        }

        // Métodos GET permanecem inalterados
        public async Task<int> GetProjectCountAsync()
        {
            var url = $"https://dev.azure.com/{_organization}/_apis/projects?api-version=7.0";
            var response = await _httpClient.GetStringAsync(url);
            var json = JObject.Parse(response);
            return json["value"]?.Count() ?? 0;
        }

        public async Task<int> GetUserCountAsync()
        {
            var url = $"https://vsaex.dev.azure.com/{_organization}/_apis/userentitlements?api-version=7.0-preview.3";
            var response = await _httpClient.GetStringAsync(url);
            var json = JObject.Parse(response);
            return json["members"]?.Count() ?? 0;
        }

        public async Task<string> GetUserLicenseAsync(string userPrincipalName)
        {
            var url = $"https://vsaex.dev.azure.com/{_organization}/_apis/userentitlements?api-version=7.0-preview.3";
            var response = await _httpClient.GetStringAsync(url);
            var json = JObject.Parse(response);

            var user = json["members"]?
                .FirstOrDefault(u => u["user"]?["principalName"]?.ToString() == userPrincipalName);

            return user?["accessLevel"]?["accountLicenseType"]?.ToString() ?? "Não encontrado";
        }

        public async Task<List<string>> GetProjectAdministratorsAsync(string projectId)
        {
            var nsUrl = $"https://dev.azure.com/{_organization}/_apis/securitynamespaces?api-version=7.0";
            var nsResponse = await _httpClient.GetStringAsync(nsUrl);
            var nsJson = JObject.Parse(nsResponse);

            var projectNamespace = nsJson["value"]?
                .FirstOrDefault(n => n["name"]?.ToString() == "Project");

            if (projectNamespace == null)
                return new List<string>();

            var namespaceId = projectNamespace["namespaceId"]?.ToString();

            if (string.IsNullOrEmpty(namespaceId))
                return new List<string>();

            var securityToken = $"$PROJECT:{projectId}";
            var aclUrl = $"https://dev.azure.com/{_organization}/_apis/accesscontrollists/{namespaceId}?token={securityToken}&api-version=7.0";
            var aclResponse = await _httpClient.GetStringAsync(aclUrl);
            var aclJson = JObject.Parse(aclResponse);

            var admins = new List<string>();

            if (aclJson["value"] != null && aclJson["value"]!.Any())
            {
                foreach (var acl in aclJson["value"]!)
                {
                    var acesDictionary = acl["acesDictionary"] as JObject;

                    if (acesDictionary != null)
                    {
                        foreach (var kvp in acesDictionary)
                        {
                            var descriptor = kvp.Key;
                            var ace = kvp.Value;

                            var allow = ace?["allow"]?.Value<int>() ?? 0;

                            if ((allow & 32768) != 0)
                            {
                                admins.Add(descriptor);
                            }
                        }
                    }
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

        public async Task<AuditLogResponse> GetAuditLogAsync(
            DateTime startTime,
            DateTime endTime,
            int batchSize = 100,
            string? continuationToken = null)
        {
            var startTimeStr = startTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var endTimeStr = endTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            var url = $"https://auditservice.dev.azure.com/{_organization}/_apis/audit/auditlog?startTime={startTimeStr}&endTime={endTimeStr}&batchSize={batchSize}&api-version=7.2-preview.1";

            if (!string.IsNullOrEmpty(continuationToken))
            {
                url += $"&continuationToken={continuationToken}";
            }

            var httpResponse = await _httpClient.GetAsync(url);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Erro ao consultar o Audit Log. Status: {httpResponse.StatusCode}. " +
                    $"Detalhes: {errorContent}. " +
                    $"Verifique se: 1) A organização '{_organization}' está correta, " +
                    $"2) O PAT tem permissões de 'Audit Log' (Read), " +
                    $"3) O Audit Log está habilitado para a organização.");
            }

            var response = await httpResponse.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(response) || response.TrimStart().StartsWith("<"))
            {
                throw new InvalidOperationException(
                    $"A resposta da API não é um JSON válido. " +
                    $"Resposta recebida: {response.Substring(0, Math.Min(200, response.Length))}...");
            }

            var json = JObject.Parse(response);

            var auditEntries = new List<AuditEntry>();

            if (json["decoratedAuditLogEntries"] != null)
            {
                foreach (var entry in json["decoratedAuditLogEntries"]!)
                {
                    auditEntries.Add(new AuditEntry
                    {
                        Id = entry["id"]?.ToString(),
                        CorrelationId = entry["correlationId"]?.ToString(),
                        ActivityId = entry["activityId"]?.ToString(),
                        ActorCUID = entry["actorCUID"]?.ToString(),
                        ActorUserId = entry["actorUserId"]?.ToString(),
                        ActorDisplayName = entry["actorDisplayName"]?.ToString(),
                        ActorUPN = entry["actorUPN"]?.ToString(),
                        AuthenticationMechanism = entry["authenticationMechanism"]?.ToString(),
                        Timestamp = entry["timestamp"]?.ToObject<DateTime>() ?? DateTime.MinValue,
                        ScopeType = entry["scopeType"]?.ToString(),
                        ScopeDisplayName = entry["scopeDisplayName"]?.ToString(),
                        ScopeId = entry["scopeId"]?.ToString(),
                        ProjectId = entry["projectId"]?.ToString(),
                        ProjectName = entry["projectName"]?.ToString(),
                        IpAddress = entry["ipAddress"]?.ToString(),
                        UserAgent = entry["userAgent"]?.ToString(),
                        ActionId = entry["actionId"]?.ToString(),
                        Data = entry["data"]?.ToString(),
                        Details = entry["details"]?.ToString(),
                        Area = entry["area"]?.ToString(),
                        Category = entry["category"]?.ToString(),
                        CategoryDisplayName = entry["categoryDisplayName"]?.ToString(),
                        ActorImageUrl = entry["actorImageUrl"]?.ToString()
                    });
                }
            }

            return new AuditLogResponse
            {
                HasMore = json["hasMore"]?.ToObject<bool>() ?? false,
                ContinuationToken = json["continuationToken"]?.ToString(),
                DecoratedAuditLogEntries = auditEntries,
                TotalCount = auditEntries.Count
            };
        }

        #region Gerenciamento de Usuários e Grupos

        public async Task<List<GraphUser>> GetUsersAsync()
        {
            var url = $"https://vssps.dev.azure.com/{_organization}/_apis/graph/users?api-version=7.0-preview.1";
            var httpResponse = await _httpClient.GetAsync(url);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Erro ao listar usuários. Status: {httpResponse.StatusCode}. Detalhes: {errorContent}");
            }

            var response = await httpResponse.Content.ReadAsStringAsync();
            var json = JObject.Parse(response);

            var users = new List<GraphUser>();

            if (json["value"] != null)
            {
                foreach (var user in json["value"]!)
                {
                    users.Add(new GraphUser
                    {
                        Descriptor = user["descriptor"]?.ToString(),
                        DisplayName = user["displayName"]?.ToString(),
                        PrincipalName = user["principalName"]?.ToString(),
                        MailAddress = user["mailAddress"]?.ToString(),
                        Origin = user["origin"]?.ToString(),
                        OriginId = user["originId"]?.ToString(),
                        SubjectKind = user["subjectKind"]?.ToString(),
                        Domain = user["domain"]?.ToString(),
                        DirectoryAlias = user["directoryAlias"]?.ToString()
                    });
                }
            }

            return users;
        }

        public async Task<GraphUser?> GetUserByEmailAsync(string email)
        {
            var users = await GetUsersAsync();
            return users.FirstOrDefault(u =>
                u.MailAddress?.Equals(email, StringComparison.OrdinalIgnoreCase) == true ||
                u.PrincipalName?.Equals(email, StringComparison.OrdinalIgnoreCase) == true);
        }

        public async Task<List<GraphGroup>> GetGroupsAsync()
        {
            var url = $"https://vssps.dev.azure.com/{_organization}/_apis/graph/groups?api-version=7.0-preview.1";
            var httpResponse = await _httpClient.GetAsync(url);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Erro ao listar grupos. Status: {httpResponse.StatusCode}. Detalhes: {errorContent}");
            }

            var response = await httpResponse.Content.ReadAsStringAsync();
            var json = JObject.Parse(response);

            var groups = new List<GraphGroup>();

            if (json["value"] != null)
            {
                foreach (var group in json["value"]!)
                {
                    groups.Add(new GraphGroup
                    {
                        Descriptor = group["descriptor"]?.ToString(),
                        DisplayName = group["displayName"]?.ToString(),
                        PrincipalName = group["principalName"]?.ToString(),
                        MailAddress = group["mailAddress"]?.ToString(),
                        Origin = group["origin"]?.ToString(),
                        OriginId = group["originId"]?.ToString(),
                        SubjectKind = group["subjectKind"]?.ToString(),
                        Domain = group["domain"]?.ToString(),
                        Description = group["description"]?.ToString()
                    });
                }
            }

            return groups;
        }

        public async Task<GraphGroup?> GetGroupByNameAsync(string groupName)
        {
            var groups = await GetGroupsAsync();
            return groups.FirstOrDefault(g =>
                g.DisplayName?.Equals(groupName, StringComparison.OrdinalIgnoreCase) == true ||
                g.PrincipalName?.Equals(groupName, StringComparison.OrdinalIgnoreCase) == true);
        }

        /// <summary>
        /// Adiciona um usuário a um grupo
        /// </summary>
        /// <param name="groupDescriptor">Descriptor do grupo</param>
        /// <param name="userDescriptor">Descriptor do usuário</param>
        /// <returns>True se a operação foi bem-sucedida</returns>
        public async Task<bool> AddUserToGroupAsync(string groupDescriptor, string userDescriptor)
        {
            // ✅ FEATURE FLAG: Verificar se operações de gerenciamento de usuários/grupos estão habilitadas
            await EnsureFeatureEnabledAsync("UserGroupManagement");

            var url = $"https://vssps.dev.azure.com/{_organization}/_apis/graph/memberships/{userDescriptor}/{groupDescriptor}?api-version=7.0-preview.1";

            _logger?.LogInformation($"Tentando adicionar usuário ao grupo. URL: {url}");
            _logger?.LogInformation($"User Descriptor: {userDescriptor}");
            _logger?.LogInformation($"Group Descriptor: {groupDescriptor}");

            var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
            var httpResponse = await _httpClient.PutAsync(url, content);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                _logger?.LogError($"Erro 400 - Detalhes completos: {errorContent}");

                throw new HttpRequestException(
                    $"Erro ao adicionar usuário ao grupo. Status: {httpResponse.StatusCode}. " +
                    $"Detalhes: {errorContent}. " +
                    $"Verifique se o PAT tem permissões de 'Graph' (Read & Manage).");
            }

            return true;
        }

        /// <summary>
        /// Remove um usuário de um grupo
        /// </summary>
        /// <param name="groupDescriptor">Descriptor do grupo</param>
        /// <param name="userDescriptor">Descriptor do usuário</param>
        /// <returns>True se a operação foi bem-sucedida</returns>
        public async Task<bool> RemoveUserFromGroupAsync(string groupDescriptor, string userDescriptor)
        {
            // ✅ FEATURE FLAG: Verificar se operações de gerenciamento de usuários/grupos estão habilitadas
            await EnsureFeatureEnabledAsync("UserGroupManagement");

            var url = $"https://vssps.dev.azure.com/{_organization}/_apis/graph/memberships/{userDescriptor}/{groupDescriptor}?api-version=7.0-preview.1";

            var httpResponse = await _httpClient.DeleteAsync(url);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Erro ao remover usuário do grupo. Status: {httpResponse.StatusCode}. " +
                    $"Detalhes: {errorContent}. " +
                    $"Verifique se o PAT tem permissões de 'Graph' (Read & Manage).");
            }

            return true;
        }

        public async Task<List<GraphMember>> GetGroupMembersAsync(string groupDescriptor)
        {
            var url = $"https://vssps.dev.azure.com/{_organization}/_apis/graph/memberships/{groupDescriptor}?direction=down&api-version=7.0-preview.1";
            var httpResponse = await _httpClient.GetAsync(url);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Erro ao listar membros do grupo. Status: {httpResponse.StatusCode}. Detalhes: {errorContent}");
            }

            var response = await httpResponse.Content.ReadAsStringAsync();
            var json = JObject.Parse(response);

            var members = new List<GraphMember>();

            if (json["value"] != null)
            {
                foreach (var member in json["value"]!)
                {
                    members.Add(new GraphMember
                    {
                        MemberDescriptor = member["memberDescriptor"]?.ToString(),
                        ContainerDescriptor = member["containerDescriptor"]?.ToString()
                    });
                }
            }

            return members;
        }

        public async Task<ProjectsResponse> GetProjectsAsync(string stateFilter = "wellFormed", int? top = null, int? skip = null)
        {
            var url = $"https://dev.azure.com/{_organization}/_apis/projects?stateFilter={stateFilter}&api-version=7.0";

            if (top.HasValue)
            {
                url += $"&$top={top.Value}";
            }

            if (skip.HasValue)
            {
                url += $"&$skip={skip.Value}";
            }

            var httpResponse = await _httpClient.GetAsync(url);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Erro ao listar projetos. Status: {httpResponse.StatusCode}. Detalhes: {errorContent}");
            }

            var response = await httpResponse.Content.ReadAsStringAsync();
            var json = JObject.Parse(response);

            var projects = new List<AzureDevOpsProject>();

            if (json["value"] != null)
            {
                foreach (var project in json["value"]!)
                {
                    projects.Add(new AzureDevOpsProject
                    {
                        Id = project["id"]?.ToString(),
                        Name = project["name"]?.ToString(),
                        Description = project["description"]?.ToString(),
                        Url = project["url"]?.ToString(),
                        State = project["state"]?.ToString(),
                        Revision = project["revision"]?.ToObject<int>() ?? 0,
                        Visibility = project["visibility"]?.ToString(),
                        LastUpdateTime = project["lastUpdateTime"]?.ToObject<DateTime>() ?? DateTime.MinValue
                    });
                }
            }

            return new ProjectsResponse
            {
                Count = json["count"]?.ToObject<int>() ?? projects.Count,
                Projects = projects
            };
        }

        public async Task<AzureDevOpsProjectDetails> GetProjectDetailsAsync(
            string projectIdOrName,
            bool includeCapabilities = false,
            bool includeHistory = false)
        {
            var url = $"https://dev.azure.com/{_organization}/_apis/projects/{projectIdOrName}?api-version=7.0";

            if (includeCapabilities)
            {
                url += "&includeCapabilities=true";
            }

            if (includeHistory)
            {
                url += "&includeHistory=true";
            }

            var httpResponse = await _httpClient.GetAsync(url);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Erro ao obter detalhes do projeto '{projectIdOrName}'. Status: {httpResponse.StatusCode}. Detalhes: {errorContent}");
            }

            var response = await httpResponse.Content.ReadAsStringAsync();
            var json = JObject.Parse(response);

            var projectDetails = new AzureDevOpsProjectDetails
            {
                Id = json["id"]?.ToString(),
                Name = json["name"]?.ToString(),
                Description = json["description"]?.ToString(),
                Url = json["url"]?.ToString(),
                State = json["state"]?.ToString(),
                Revision = json["revision"]?.ToObject<int>() ?? 0,
                Visibility = json["visibility"]?.ToString(),
                LastUpdateTime = json["lastUpdateTime"]?.ToObject<DateTime>() ?? DateTime.MinValue,
                DefaultTeamImageUrl = json["defaultTeamImageUrl"]?.ToString()
            };

            if (json["capabilities"] != null)
            {
                var capabilities = new Dictionary<string, Dictionary<string, string>>();

                foreach (var capability in json["capabilities"]!)
                {
                    var capProp = (JProperty)capability;
                    var capDict = new Dictionary<string, string>();

                    foreach (var item in capProp.Value)
                    {
                        var itemProp = (JProperty)item;
                        capDict[itemProp.Name] = itemProp.Value?.ToString() ?? string.Empty;
                    }

                    capabilities[capProp.Name] = capDict;
                }

                projectDetails.Capabilities = capabilities;
            }

            return projectDetails;
        }

        #endregion

        #region Gerenciamento de Work Items

        /// <summary>
        /// Cria um novo Work Item em um projeto
        /// </summary>
        /// <param name="projectIdOrName">ID ou nome do projeto</param>
        /// <param name="workItemType">Tipo do Work Item (Bug, Task, User Story, etc.)</param>
        /// <param name="fields">Dicionário com os campos do Work Item</param>
        /// <returns>Work Item criado</returns>
        public async Task<WorkItem> CreateWorkItemAsync(
            string projectIdOrName,
            string workItemType,
            Dictionary<string, object> fields)
        {
            // ✅ FEATURE FLAG: Verificar se criação de work items está habilitada
            await EnsureFeatureEnabledAsync("WorkItemCreation");

            // Validar que System.Title existe
            if (!fields.ContainsKey("System.Title") || string.IsNullOrWhiteSpace(fields["System.Title"]?.ToString()))
            {
                throw new ArgumentException("O campo 'System.Title' é obrigatório para criar um Work Item.");
            }

            var url = $"https://dev.azure.com/{_organization}/{projectIdOrName}/_apis/wit/workitems/${workItemType}?api-version=7.0";

            // Criar o payload no formato JSON Patch
            var patchDocument = new List<object>();
            foreach (var field in fields)
            {
                patchDocument.Add(new
                {
                    op = "add",
                    path = $"/fields/{field.Key}",
                    value = field.Value
                });
            }

            var jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(patchDocument);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json-patch+json");

            // Log the request for debugging
            _logger?.LogDebug("Creating Work Item - URL: {Url}, Payload: {Payload}", url, jsonContent);

            var httpResponse = await _httpClient.PostAsync(url, content);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();

                // Log detalhes completos do erro
                _logger?.LogError(
                    "Erro ao criar Work Item. Status: {StatusCode}, URL: {Url}, Payload: {Payload}, Resposta: {Response}",
                    httpResponse.StatusCode, url, jsonContent, errorContent);

                throw new HttpRequestException(
                    $"Erro ao criar Work Item. Status: {httpResponse.StatusCode}. " +
                    $"Detalhes: {errorContent}. " +
                    $"Payload enviado: {jsonContent}. " +
                    $"Verifique se: 1) O PAT tem permissões de 'Work Items' (Read, Write & Manage), " +
                    $"2) O campo 'System.Title' está presente, " +
                    $"3) O tipo de Work Item '{workItemType}' existe no projeto '{projectIdOrName}'.");
            }

            var response = await httpResponse.Content.ReadAsStringAsync();
            var json = JObject.Parse(response);

            return ParseWorkItem(json);
        }

        public async Task<WorkItem> GetWorkItemAsync(
            int workItemId,
            List<string>? fields = null,
            string expand = "All")
        {
            var url = $"https://dev.azure.com/{_organization}/_apis/wit/workitems/{workItemId}?api-version=7.0";

            if (fields != null && fields.Any())
            {
                url += $"&fields={string.Join(",", fields)}";
            }

            if (!string.IsNullOrEmpty(expand))
            {
                url += $"&$expand={expand}";
            }

            var httpResponse = await _httpClient.GetAsync(url);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Erro ao obter Work Item {workItemId}. Status: {httpResponse.StatusCode}. " +
                    $"Detalhes: {errorContent}");
            }

            var response = await httpResponse.Content.ReadAsStringAsync();
            var json = JObject.Parse(response);

            return ParseWorkItem(json);
        }

        public async Task<List<WorkItem>> GetWorkItemsAsync(
            List<int> workItemIds,
            List<string>? fields = null)
        {
            if (!workItemIds.Any())
                return new List<WorkItem>();

            var url = $"https://dev.azure.com/{_organization}/_apis/wit/workitems?ids={string.Join(",", workItemIds)}&api-version=7.0";

            if (fields != null && fields.Any())
            {
                url += $"&fields={string.Join(",", fields)}";
            }

            var httpResponse = await _httpClient.GetAsync(url);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Erro ao obter Work Items. Status: {httpResponse.StatusCode}. " +
                    $"Detalhes: {errorContent}");
            }

            var response = await httpResponse.Content.ReadAsStringAsync();
            var json = JObject.Parse(response);

            var workItems = new List<WorkItem>();

            if (json["value"] != null)
            {
                foreach (var item in json["value"]!)
                {
                    workItems.Add(ParseWorkItem(item));
                }
            }

            return workItems;
        }

        /// <summary>
        /// Executa uma query WIQL (Work Item Query Language) para buscar Work Items
        /// </summary>
        /// <param name="projectIdOrName">ID ou nome do projeto</param>
        /// <param name="wiql">Query WIQL</param>
        /// <returns>Lista de Work Items encontrados</returns>
        public async Task<WorkItemQueryResult> QueryWorkItemsAsync(string projectIdOrName, string wiql)
        {
            // ✅ FEATURE FLAG: Verificar se queries de work items estão habilitadas
            await EnsureFeatureEnabledAsync("WorkItemQuery");

            var url = $"https://dev.azure.com/{_organization}/{projectIdOrName}/_apis/wit/wiql?api-version=7.0";

            var payload = new { query = wiql };
            var jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var httpResponse = await _httpClient.PostAsync(url, content);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Erro ao executar query WIQL. Status: {httpResponse.StatusCode}. " +
                    $"Detalhes: {errorContent}");
            }

            var response = await httpResponse.Content.ReadAsStringAsync();
            var json = JObject.Parse(response);

            var result = new WorkItemQueryResult
            {
                QueryType = json["queryType"]?.ToString(),
                QueryResultType = json["queryResultType"]?.ToString(),
                AsOf = json["asOf"]?.ToObject<DateTime>() ?? DateTime.MinValue,
                WorkItemIds = new List<int>(),
                WorkItems = new List<WorkItem>()
            };

            if (json["workItems"] != null)
            {
                foreach (var item in json["workItems"]!)
                {
                    var id = item["id"]?.ToObject<int>();
                    if (id.HasValue)
                    {
                        result.WorkItemIds.Add(id.Value);
                    }
                }
            }

            // Se há IDs, buscar os Work Items completos
            if (result.WorkItemIds.Any())
            {
                result.WorkItems = await GetWorkItemsAsync(result.WorkItemIds);
            }

            return result;
        }

        /// <summary>
        /// Atualiza um Work Item existente
        /// </summary>
        /// <param name="workItemId">ID do Work Item</param>
        /// <param name="fields">Dicionário com os campos a atualizar</param>
        /// <returns>Work Item atualizado</returns>
        public async Task<WorkItem> UpdateWorkItemAsync(
            int workItemId,
            Dictionary<string, object> fields)
        {
            // ✅ FEATURE FLAG: Verificar se atualização de work items está habilitada
            await EnsureFeatureEnabledAsync("WorkItemUpdate");

            var url = $"https://dev.azure.com/{_organization}/_apis/wit/workitems/{workItemId}?api-version=7.0";

            // Criar o payload no formato JSON Patch
            var patchDocument = new List<object>();
            foreach (var field in fields)
            {
                patchDocument.Add(new
                {
                    op = "replace",
                    path = $"/fields/{field.Key}",
                    value = field.Value
                });
            }

            var jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(patchDocument);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json-patch+json");

            var httpResponse = await _httpClient.PatchAsync(url, content);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Erro ao atualizar Work Item {workItemId}. Status: {httpResponse.StatusCode}. " +
                    $"Detalhes: {errorContent}");
            }

            var response = await httpResponse.Content.ReadAsStringAsync();
            var json = JObject.Parse(response);

            return ParseWorkItem(json);
        }

        /// <summary>
        /// Deleta um Work Item
        /// </summary>
        /// <param name="workItemId">ID do Work Item</param>
        /// <param name="destroy">Se true, deleta permanentemente; se false, move para a lixeira</param>
        /// <returns>True se a operação foi bem-sucedida</returns>
        public async Task<bool> DeleteWorkItemAsync(
            int workItemId,
            bool destroy = false)
        {
            // ✅ FEATURE FLAG: Verificar se deleção de work items está habilitada (mais crítico!)
            await EnsureFeatureEnabledAsync("WorkItemDeletion");

            var url = $"https://dev.azure.com/{_organization}/_apis/wit/workitems/{workItemId}?api-version=7.0";

            if (destroy)
            {
                url += "&destroy=true";
            }

            var httpResponse = await _httpClient.DeleteAsync(url);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Erro ao deletar Work Item {workItemId}. Status: {httpResponse.StatusCode}. " +
                    $"Detalhes: {errorContent}");
            }

            return true;
        }

        private WorkItem ParseWorkItem(JToken json)
        {
            var workItem = new WorkItem
            {
                Id = json["id"]?.ToObject<int>() ?? 0,
                Rev = json["rev"]?.ToObject<int>() ?? 0,
                Url = json["url"]?.ToString(),
                Fields = new Dictionary<string, object>()
            };

            // Parse dos campos
            if (json["fields"] != null)
            {
                foreach (var field in json["fields"]!)
                {
                    var fieldProp = (JProperty)field;
                    var fieldValue = fieldProp.Value;

                    // Tentar converter para tipos apropriados
                    object value;
                    if (fieldValue.Type == JTokenType.Date)
                    {
                        value = fieldValue.ToObject<DateTime>();
                    }
                    else if (fieldValue.Type == JTokenType.Integer)
                    {
                        value = fieldValue.ToObject<int>();
                    }
                    else if (fieldValue.Type == JTokenType.Float)
                    {
                        value = fieldValue.ToObject<double>();
                    }
                    else if (fieldValue.Type == JTokenType.Boolean)
                    {
                        value = fieldValue.ToObject<bool>();
                    }
                    else
                    {
                        value = fieldValue.ToString();
                    }

                    workItem.Fields[fieldProp.Name] = value;
                }
            }

            // Parse das relações se existirem
            if (json["relations"] != null)
            {
                workItem.Relations = new List<WorkItemRelation>();
                foreach (var relation in json["relations"]!)
                {
                    workItem.Relations.Add(new WorkItemRelation
                    {
                        Rel = relation["rel"]?.ToString(),
                        Url = relation["url"]?.ToString(),
                        Attributes = relation["attributes"]?.ToObject<Dictionary<string, object>>()
                    });
                }
            }

            return workItem;
        }

        #endregion

        public async Task<bool> AddUserToGroupByNameAsync(string groupDisplayName, string userDisplayName)
        {
            var group = await GetGroupByNameAsync(groupDisplayName);
            if (group == null)
            {
                throw new ArgumentException($"Grupo '{groupDisplayName}' não encontrado");
            }

            var users = await GetUsersAsync();
            var user = users.FirstOrDefault(u =>
                u.DisplayName?.Equals(userDisplayName, StringComparison.OrdinalIgnoreCase) == true ||
                u.MailAddress?.Equals(userDisplayName, StringComparison.OrdinalIgnoreCase) == true ||
                u.PrincipalName?.Equals(userDisplayName, StringComparison.OrdinalIgnoreCase) == true);

            if (user == null)
            {
                throw new ArgumentException($"Usuário '{userDisplayName}' não encontrado");
            }

            if (string.IsNullOrEmpty(group.Descriptor) || string.IsNullOrEmpty(user.Descriptor))
            {
                throw new InvalidOperationException("Descriptor do grupo ou usuário está vazio");
            }

            return await AddUserToGroupAsync(group.Descriptor, user.Descriptor);
        }

        public async Task<bool> RemoveUserFromGroupByNameAsync(string groupDisplayName, string userDisplayName)
        {
            var group = await GetGroupByNameAsync(groupDisplayName);
            if (group == null)
            {
                throw new ArgumentException($"Grupo '{groupDisplayName}' não encontrado");
            }

            var users = await GetUsersAsync();
            var user = users.FirstOrDefault(u =>
                u.DisplayName?.Equals(userDisplayName, StringComparison.OrdinalIgnoreCase) == true ||
                u.MailAddress?.Equals(userDisplayName, StringComparison.OrdinalIgnoreCase) == true ||
                u.PrincipalName?.Equals(userDisplayName, StringComparison.OrdinalIgnoreCase) == true);

            if (user == null)
            {
                throw new ArgumentException($"Usuário '{userDisplayName}' não encontrado");
            }

            if (string.IsNullOrEmpty(group.Descriptor) || string.IsNullOrEmpty(user.Descriptor))
            {
                throw new InvalidOperationException("Descriptor do grupo ou usuário está vazio");
            }

            return await RemoveUserFromGroupAsync(group.Descriptor, user.Descriptor);
        }

        public async Task<GroupEntitlementsResponse> GetGroupEntitlementsAsync()
        {
            var url = $"https://vsaex.dev.azure.com/{_organization}/_apis/groupentitlements?api-version=7.2-preview.1";
            var httpResponse = await _httpClient.GetAsync(url);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Erro ao listar group entitlements. Status: {httpResponse.StatusCode}. " +
                    $"Detalhes: {errorContent}. " +
                    $"Verifique se o PAT tem permissões de 'Member Entitlement Management' (Read).");
            }

            var response = await httpResponse.Content.ReadAsStringAsync();
            var json = JObject.Parse(response);

            var groupEntitlements = new List<GroupEntitlement>();

            if (json["value"] != null)
            {
                foreach (var item in json["value"]!)
                {
                    groupEntitlements.Add(ParseGroupEntitlement(item));
                }
            }

            return new GroupEntitlementsResponse
            {
                Count = json["count"]?.ToObject<int>() ?? groupEntitlements.Count,
                GroupEntitlements = groupEntitlements
            };
        }

        private GroupEntitlement ParseGroupEntitlement(JToken json)
        {
            var groupEntitlement = new GroupEntitlement
            {
                Id = json["id"]?.ToString(),
                Status = json["status"]?.ToString(),
                LastExecuted = json["lastExecuted"]?.ToObject<DateTime>()
            };

            if (json["group"] != null)
            {
                var group = json["group"]!;
                groupEntitlement.Group = new GraphGroupDetail
                {
                    Descriptor = group["descriptor"]?.ToString(),
                    DisplayName = group["displayName"]?.ToString(),
                    PrincipalName = group["principalName"]?.ToString(),
                    MailAddress = group["mailAddress"]?.ToString(),
                    Origin = group["origin"]?.ToString(),
                    OriginId = group["originId"]?.ToString(),
                    SubjectKind = group["subjectKind"]?.ToString(),
                    Domain = group["domain"]?.ToString(),
                    Description = group["description"]?.ToString(),
                    Url = group["url"]?.ToString()
                };
            }

            if (json["licenseRule"] != null)
            {
                var licenseRule = json["licenseRule"]!;
                groupEntitlement.LicenseRule = new AccessLevel
                {
                    LicensingSource = licenseRule["licensingSource"]?.ToString(),
                    AccountLicenseType = licenseRule["accountLicenseType"]?.ToString(),
                    MsdnLicenseType = licenseRule["msdnLicenseType"]?.ToString(),
                    LicenseDisplayName = licenseRule["licenseDisplayName"]?.ToString(),
                    Status = licenseRule["status"]?.ToString(),
                    StatusMessage = licenseRule["statusMessage"]?.ToString(),
                    AssignmentSource = licenseRule["assignmentSource"]?.ToString()
                };
            }

            if (json["projectEntitlements"] != null)
            {
                groupEntitlement.ProjectEntitlements = new List<ProjectEntitlementDetail>();
                foreach (var pe in json["projectEntitlements"]!)
                {
                    var projectEntitlement = new ProjectEntitlementDetail
                    {
                        AssignmentSource = pe["assignmentSource"]?.ToString(),
                        IsProjectPermissionInherited = pe["isProjectPermissionInherited"]?.ToObject<bool>() ?? false
                    };

                    // Parse da referência do projeto
                    if (pe["projectRef"] != null)
                    {
                        projectEntitlement.ProjectRef = new ProjectReference
                        {
                            Id = pe["projectRef"]!["id"]?.ToString(),
                            Name = pe["projectRef"]!["name"]?.ToString()
                        };
                    }

                    // Parse do grupo
                    if (pe["group"] != null)
                    {
                        projectEntitlement.Group = new ProjectGroupDetail
                        {
                            GroupType = pe["group"]!["groupType"]?.ToString(),
                            DisplayName = pe["group"]!["displayName"]?.ToString()
                        };
                    }

                    // Parse das referências de times
                    if (pe["teamRefs"] != null)
                    {
                        projectEntitlement.TeamRefs = new List<TeamReference>();
                        foreach (var team in pe["teamRefs"]!)
                        {
                            projectEntitlement.TeamRefs.Add(new TeamReference
                            {
                                Id = team["id"]?.ToString(),
                                Name = team["name"]?.ToString()
                            });
                        }
                    }

                    groupEntitlement.ProjectEntitlements.Add(projectEntitlement);
                }
            }

            return groupEntitlement;
        }

        #region Pipeline Operations

        /// <summary>
        /// Lista todos os pipelines de um projeto
        /// </summary>
        public async Task<List<Pipeline>> GetPipelinesAsync(string project)
        {
            var url = $"https://dev.azure.com/{_organization}/{project}/_apis/pipelines?api-version=7.2-preview.1";
            _logger?.LogInformation("Fetching pipelines for project: {Project}", project);

            var response = await _httpClient.GetStringAsync(url);
            var json = JObject.Parse(response);

            var pipelines = new List<Pipeline>();

            if (json["value"] != null)
            {
                foreach (var item in json["value"]!)
                {
                    pipelines.Add(new Pipeline
                    {
                        Id = item["id"]?.Value<int>() ?? 0,
                        Name = item["name"]?.ToString() ?? string.Empty,
                        Folder = item["folder"]?.ToString(),
                        Revision = item["revision"]?.Value<int>() ?? 0,
                        Url = item["url"]?.ToString() ?? string.Empty
                    });
                }
            }

            _logger?.LogInformation("Found {Count} pipelines", pipelines.Count);
            return pipelines;
        }

        /// <summary>
        /// Obtém detalhes de um pipeline específico
        /// </summary>
        public async Task<Pipeline?> GetPipelineAsync(string project, int pipelineId)
        {
            var url = $"https://dev.azure.com/{_organization}/{project}/_apis/pipelines/{pipelineId}?api-version=7.2-preview.1";
            _logger?.LogInformation("Fetching pipeline {PipelineId} from project {Project}", pipelineId, project);

            try
            {
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);

                return new Pipeline
                {
                    Id = json["id"]?.Value<int>() ?? 0,
                    Name = json["name"]?.ToString() ?? string.Empty,
                    Folder = json["folder"]?.ToString(),
                    Revision = json["revision"]?.Value<int>() ?? 0,
                    Url = json["url"]?.ToString() ?? string.Empty
                };
            }
            catch (HttpRequestException ex)
            {
                _logger?.LogError(ex, "Pipeline {PipelineId} not found in project {Project}", pipelineId, project);
                return null;
            }
        }

        /// <summary>
        /// Lista todas as execuções (runs) de um pipeline
        /// </summary>
        public async Task<List<PipelineRun>> GetPipelineRunsAsync(string project, int pipelineId)
        {
            var url = $"https://dev.azure.com/{_organization}/{project}/_apis/pipelines/{pipelineId}/runs?api-version=7.2-preview.1";
            _logger?.LogInformation("Fetching runs for pipeline {PipelineId} in project {Project}", pipelineId, project);

            var response = await _httpClient.GetStringAsync(url);
            var json = JObject.Parse(response);

            var runs = new List<PipelineRun>();

            if (json["value"] != null)
            {
                foreach (var item in json["value"]!)
                {
                    runs.Add(new PipelineRun
                    {
                        Id = item["id"]?.Value<int>() ?? 0,
                        Name = item["name"]?.ToString() ?? string.Empty,
                        State = item["state"]?.ToString() ?? string.Empty,
                        Result = item["result"]?.ToString() ?? string.Empty,
                        CreatedDate = item["createdDate"]?.Value<DateTime>() ?? DateTime.MinValue,
                        FinishedDate = item["finishedDate"]?.Value<DateTime?>(),
                        Url = item["url"]?.ToString() ?? string.Empty
                    });
                }
            }

            _logger?.LogInformation("Found {Count} runs for pipeline {PipelineId}", runs.Count, pipelineId);
            return runs;
        }

        /// <summary>
        /// Obtém execuções falhadas de um pipeline em um período específico
        /// </summary>
        public async Task<List<PipelineRun>> GetFailedPipelineRunsAsync(
            string project,
            int pipelineId,
            DateTime startDate,
            DateTime endDate)
        {
            _logger?.LogInformation(
                "Fetching failed runs for pipeline {PipelineId} between {StartDate} and {EndDate}",
                pipelineId, startDate, endDate);

            var allRuns = await GetPipelineRunsAsync(project, pipelineId);

            var failedRuns = allRuns
                .Where(r => r.Result.Equals("failed", StringComparison.OrdinalIgnoreCase) &&
                            r.CreatedDate >= startDate &&
                            r.CreatedDate <= endDate)
                .OrderByDescending(r => r.CreatedDate)
                .ToList();

            _logger?.LogInformation("Found {Count} failed runs", failedRuns.Count);
            return failedRuns;
        }

        /// <summary>
        /// Lista todos os logs de uma execução específica
        /// </summary>
        public async Task<LogCollection> GetPipelineLogsAsync(string project, int pipelineId, int runId)
        {
            var url = $"https://dev.azure.com/{_organization}/{project}/_apis/pipelines/{pipelineId}/runs/{runId}/logs?api-version=7.2-preview.1";
            _logger?.LogInformation("Fetching logs for run {RunId} of pipeline {PipelineId}", runId, pipelineId);

            var response = await _httpClient.GetStringAsync(url);
            var json = JObject.Parse(response);

            var logCollection = new LogCollection
            {
                Url = json["url"]?.ToString() ?? string.Empty
            };

            if (json["logs"] != null)
            {
                foreach (var item in json["logs"]!)
                {
                    logCollection.Logs.Add(new PipelineLog
                    {
                        Id = item["id"]?.Value<int>() ?? 0,
                        CreatedOn = item["createdOn"]?.Value<DateTime>() ?? DateTime.MinValue,
                        LastChangedOn = item["lastChangedOn"]?.Value<DateTime>() ?? DateTime.MinValue,
                        LineCount = item["lineCount"]?.Value<long>() ?? 0,
                        Url = item["url"]?.ToString() ?? string.Empty
                    });
                }
            }

            _logger?.LogInformation("Found {Count} logs", logCollection.Logs.Count);
            return logCollection;
        }

        /// <summary>
        /// Obtém o conteúdo de um log específico
        /// </summary>
        public async Task<string> GetLogContentAsync(string project, int pipelineId, int runId, int logId)
        {
            var url = $"https://dev.azure.com/{_organization}/{project}/_apis/pipelines/{pipelineId}/runs/{runId}/logs/{logId}?api-version=7.2-preview.1";
            _logger?.LogInformation("Fetching content of log {LogId} from run {RunId}", logId, runId);

            var response = await _httpClient.GetStringAsync(url);
            return response;
        }

        #endregion
    }

    #region DTOs de Auditoria

    public class AuditLogResponse
    {
        public bool HasMore { get; set; }
        public string? ContinuationToken { get; set; }
        public required List<AuditEntry> DecoratedAuditLogEntries { get; set; }
        public int TotalCount { get; set; }
    }

    public class AuditEntry
    {
        public string? Id { get; set; }
        public string? CorrelationId { get; set; }
        public string? ActivityId { get; set; }
        public string? ActorCUID { get; set; }
        public string? ActorUserId { get; set; }
        public string? ActorDisplayName { get; set; }
        public string? ActorUPN { get; set; }
        public string? AuthenticationMechanism { get; set; }
        public DateTime Timestamp { get; set; }
        public string? ScopeType { get; set; }
        public string? ScopeDisplayName { get; set; }
        public string? ScopeId { get; set; }
        public string? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? ActionId { get; set; }
        public string? Data { get; set; }
        public string? Details { get; set; }
        public string? Area { get; set; }
        public string? Category { get; set; }
        public string? CategoryDisplayName { get; set; }
        public string? ActorImageUrl { get; set; }
    }

    #endregion

    #region DTOs de Graph (Usuários e Grupos)

    public class GraphUser
    {
        public string? Descriptor { get; set; }
        public string? DisplayName { get; set; }
        public string? PrincipalName { get; set; }
        public string? MailAddress { get; set; }
        public string? Origin { get; set; }
        public string? OriginId { get; set; }
        public string? SubjectKind { get; set; }
        public string? Domain { get; set; }
        public string? DirectoryAlias { get; set; }
    }

    public class GraphGroup
    {
        public string? Descriptor { get; set; }
        public string? DisplayName { get; set; }
        public string? PrincipalName { get; set; }
        public string? MailAddress { get; set; }
        public string? Origin { get; set; }
        public string? OriginId { get; set; }
        public string? SubjectKind { get; set; }
        public string? Domain { get; set; }
        public string? Description { get; set; }
    }

    public class GraphMember
    {
        public string? MemberDescriptor { get; set; }
        public string? ContainerDescriptor { get; set; }
    }

    public class ProjectsResponse
    {
        public int Count { get; set; }
        public required List<AzureDevOpsProject> Projects { get; set; }
    }

    public class AzureDevOpsProject
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Url { get; set; }
        public string? State { get; set; }
        public int Revision { get; set; }
        public string? Visibility { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }

    public class AzureDevOpsProjectDetails : AzureDevOpsProject
    {
        public string? DefaultTeamImageUrl { get; set; }
        public Dictionary<string, Dictionary<string, string>>? Capabilities { get; set; }
    }

    #endregion

    #region DTOs de Work Items

    public class WorkItem
    {
        public int Id { get; set; }
        public int Rev { get; set; }
        public string? Url { get; set; }
        public required Dictionary<string, object> Fields { get; set; }
        public List<WorkItemRelation>? Relations { get; set; }
    }

    public class WorkItemRelation
    {
        public string? Rel { get; set; }
        public string? Url { get; set; }
        public Dictionary<string, object>? Attributes { get; set; }
    }

    public class WorkItemQueryResult
    {
        public string? QueryType { get; set; }
        public string? QueryResultType { get; set; }
        public DateTime AsOf { get; set; }
        public required List<int> WorkItemIds { get; set; }
        public required List<WorkItem> WorkItems { get; set; }
    }

    #endregion

    #region DTOs de Group Entitlements

    public class GroupEntitlementsResponse
    {
        public int Count { get; set; }
        public required List<GroupEntitlement> GroupEntitlements { get; set; }
    }

    public class GroupEntitlement
    {
        public string? Id { get; set; }
        public GraphGroupDetail? Group { get; set; }
        public AccessLevel? LicenseRule { get; set; }
        public List<ProjectEntitlementDetail>? ProjectEntitlements { get; set; }
        public string? Status { get; set; }
        public DateTime? LastExecuted { get; set; }
    }

    public class GraphGroupDetail
    {
        public string? Descriptor { get; set; }
        public string? DisplayName { get; set; }
        public string? PrincipalName { get; set; }
        public string? MailAddress { get; set; }
        public string? Origin { get; set; }
        public string? OriginId { get; set; }
        public string? SubjectKind { get; set; }
        public string? Domain { get; set; }
        public string? Description { get; set; }
        public string? Url { get; set; }
    }

    public class AccessLevel
    {
        public string? LicensingSource { get; set; }
        public string? AccountLicenseType { get; set; }
        public string? MsdnLicenseType { get; set; }
        public string? LicenseDisplayName { get; set; }
        public string? Status { get; set; }
        public string? StatusMessage { get; set; }
        public string? AssignmentSource { get; set; }
    }

    public class ProjectEntitlementDetail
    {
        public ProjectReference? ProjectRef { get; set; }
        public ProjectGroupDetail? Group { get; set; }
        public bool IsProjectPermissionInherited { get; set; }
        public List<TeamReference>? TeamRefs { get; set; }
        public string? AssignmentSource { get; set; }
    }

    public class ProjectReference
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    public class ProjectGroupDetail
    {
        public string? GroupType { get; set; }
        public string? DisplayName { get; set; }
    }

    public class TeamReference
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }
}

    #endregion