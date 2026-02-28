using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;

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

        /// <summary>
        /// Consulta o audit log do Azure DevOps para um período específico
        /// </summary>
        /// <param name="startTime">Data/hora inicial da consulta</param>
        /// <param name="endTime">Data/hora final da consulta</param>
        /// <param name="batchSize">Número de registros por página (padrão: 100)</param>
        /// <param name="continuationToken">Token para paginação</param>
        /// <returns>Dados do audit log</returns>
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

            // Usar HttpResponseMessage para capturar melhor os erros
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

            // Validar se a resposta é JSON válido
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
                foreach (var entry in json["decoratedAuditLogEntries"])
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

        /// <summary>
        /// Lista todos os usuários da organização
        /// </summary>
        /// <returns>Lista de usuários</returns>
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
                foreach (var user in json["value"])
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

        /// <summary>
        /// Busca um usuário específico por email
        /// </summary>
        /// <param name="email">Email do usuário</param>
        /// <returns>Usuário encontrado ou null</returns>
        public async Task<GraphUser?> GetUserByEmailAsync(string email)
        {
            var users = await GetUsersAsync();
            return users.FirstOrDefault(u =>
                u.MailAddress?.Equals(email, StringComparison.OrdinalIgnoreCase) == true ||
                u.PrincipalName?.Equals(email, StringComparison.OrdinalIgnoreCase) == true);
        }

        /// <summary>
        /// Lista todos os grupos da organização
        /// </summary>
        /// <returns>Lista de grupos</returns>
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
                foreach (var group in json["value"])
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

        /// <summary>
        /// Busca um grupo específico por nome
        /// </summary>
        /// <param name="groupName">Nome do grupo</param>
        /// <returns>Grupo encontrado ou null</returns>
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
            var url = $"https://vssps.dev.azure.com/{_organization}/_apis/graph/memberships/{userDescriptor}/{groupDescriptor}?api-version=7.0-preview.1";

            var httpResponse = await _httpClient.PutAsync(url, new StringContent(string.Empty));

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
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

        /// <summary>
        /// Lista os membros de um grupo específico
        /// </summary>
        /// <param name="groupDescriptor">Descriptor do grupo</param>
        /// <returns>Lista de membros do grupo</returns>
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
                foreach (var member in json["value"])
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

        /// <summary>
        /// Lista todos os projetos da organização Azure DevOps
        /// </summary>
        /// <param name="stateFilter">Filtro de estado: 'all', 'wellFormed', 'createPending', 'deleting', 'new' ou 'unchanged'</param>
        /// <param name="top">Número máximo de projetos a retornar</param>
        /// <param name="skip">Número de projetos a pular (para paginação)</param>
        /// <returns>Lista de projetos</returns>
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
                foreach (var project in json["value"])
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

        /// <summary>
        /// Obtém detalhes de um projeto específico pelo ID ou nome
        /// </summary>
        /// <param name="projectIdOrName">ID ou nome do projeto</param>
        /// <param name="includeCapabilities">Incluir informações de capacidades do projeto</param>
        /// <param name="includeHistory">Incluir histórico do projeto</param>
        /// <returns>Detalhes do projeto</returns>
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

            // Processar capabilities se disponível
            if (json["capabilities"] != null)
            {
                var capabilities = new Dictionary<string, Dictionary<string, string>>();
                
                foreach (var capability in json["capabilities"])
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
    }

    #region DTOs de Auditoria

    /// <summary>
    /// Modelo de resposta do audit log
    /// </summary>
    public class AuditLogResponse
    {
        /// <summary>
        /// Indica se existem mais registros disponíveis
        /// </summary>
        public bool HasMore { get; set; }

        /// <summary>
        /// Token para buscar a próxima página de resultados
        /// </summary>
        public string? ContinuationToken { get; set; }

        /// <summary>
        /// Lista de entradas do audit log
        /// </summary>
        public required List<AuditEntry> DecoratedAuditLogEntries { get; set; }

        /// <summary>
        /// Número total de registros retornados
        /// </summary>
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// Entrada individual do audit log
    /// </summary>
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

    /// <summary>
    /// Representa um usuário do Azure DevOps
    /// </summary>
    public class GraphUser
    {
        /// <summary>
        /// Descriptor único do usuário (usado para operações de API)
        /// </summary>
        public string? Descriptor { get; set; }

        /// <summary>
        /// Nome de exibição do usuário
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// User Principal Name (UPN)
        /// </summary>
        public string? PrincipalName { get; set; }

        /// <summary>
        /// Endereço de email
        /// </summary>
        public string? MailAddress { get; set; }

        /// <summary>
        /// Origem do usuário (aad, vsts, etc.)
        /// </summary>
        public string? Origin { get; set; }

        /// <summary>
        /// ID de origem
        /// </summary>
        public string? OriginId { get; set; }

        /// <summary>
        /// Tipo de entidade (user)
        /// </summary>
        public string? SubjectKind { get; set; }

        /// <summary>
        /// Domínio do usuário
        /// </summary>
        public string? Domain { get; set; }

        /// <summary>
        /// Alias do diretório
        /// </summary>
        public string? DirectoryAlias { get; set; }
    }

    /// <summary>
    /// Representa um grupo do Azure DevOps
    /// </summary>
    public class GraphGroup
    {
        /// <summary>
        /// Descriptor único do grupo (usado para operações de API)
        /// </summary>
        public string? Descriptor { get; set; }

        /// <summary>
        /// Nome de exibição do grupo
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Nome principal do grupo
        /// </summary>
        public string? PrincipalName { get; set; }

        /// <summary>
        /// Endereço de email do grupo
        /// </summary>
        public string? MailAddress { get; set; }

        /// <summary>
        /// Origem do grupo (vsts, aad, etc.)
        /// </summary>
        public string? Origin { get; set; }

        /// <summary>
        /// ID de origem
        /// </summary>
        public string? OriginId { get; set; }

        /// <summary>
        /// Tipo de entidade (group)
        /// </summary>
        public string? SubjectKind { get; set; }

        /// <summary>
        /// Domínio do grupo
        /// </summary>
        public string? Domain { get; set; }

        /// <summary>
        /// Descrição do grupo
        /// </summary>
        public string? Description { get; set; }
    }

    /// <summary>
    /// Representa um membro de um grupo
    /// </summary>
    public class GraphMember
    {
        /// <summary>
        /// Descriptor do membro
        /// </summary>
        public string? MemberDescriptor { get; set; }

        /// <summary>
        /// Descriptor do container (grupo)
        /// </summary>
        public string? ContainerDescriptor { get; set; }
    }

    /// <summary>
    /// Resposta da listagem de projetos
    /// </summary>
    public class ProjectsResponse
    {
        /// <summary>
        /// Número total de projetos retornados
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Lista de projetos
        /// </summary>
        public required List<AzureDevOpsProject> Projects { get; set; }
    }

    /// <summary>
    /// Representa um projeto do Azure DevOps
    /// </summary>
    public class AzureDevOpsProject
    {
        /// <summary>
        /// ID único do projeto (GUID)
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Nome do projeto
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Descrição do projeto
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// URL do projeto
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Estado do projeto (wellFormed, createPending, deleting, new, unchanged)
        /// </summary>
        public string? State { get; set; }

        /// <summary>
        /// Número de revisão do projeto
        /// </summary>
        public int Revision { get; set; }

        /// <summary>
        /// Visibilidade do projeto (private, public)
        /// </summary>
        public string? Visibility { get; set; }

        /// <summary>
        /// Data/hora da última atualização do projeto
        /// </summary>
        public DateTime LastUpdateTime { get; set; }
    }

    /// <summary>
    /// Representa detalhes completos de um projeto do Azure DevOps
    /// </summary>
    public class AzureDevOpsProjectDetails : AzureDevOpsProject
    {
        /// <summary>
        /// URL da imagem do time padrão
        /// </summary>
        public string? DefaultTeamImageUrl { get; set; }

        /// <summary>
        /// Capacidades do projeto (versioncontrol, processTemplate, etc.)
        /// </summary>
        public Dictionary<string, Dictionary<string, string>>? Capabilities { get; set; }
    }

    #endregion
}