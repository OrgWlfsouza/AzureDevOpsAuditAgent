using AzureDevOpsAuditAgent.Class;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuditController : ControllerBase
{
    private readonly AzureDevOpsService _service;

    public AuditController(AzureDevOpsService service)
    {
        _service = service;
    }

    /// <summary>
    /// Obtém a contagem total de projetos na organização Azure DevOps
    /// </summary>
    /// <returns>Número total de projetos</returns>
    /// <response code="200">Retorna a contagem de projetos</response>
    /// <response code="400">Requisição inválida</response>
    /// <response code="500">Erro interno do servidor</response>
    [HttpGet("projects/count")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ProjectCountResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> GetProjectCount()
    {
        var count = await _service.GetProjectCountAsync();
        return Ok(new ProjectCountResponse { Projects = count });
    }

    /// <summary>
    /// Obtém a contagem total de usuários cadastrados na organização
    /// </summary>
    /// <returns>Número total de usuários</returns>
    /// <response code="200">Retorna a contagem de usuários</response>
    /// <response code="400">Requisição inválida</response>
    /// <response code="500">Erro interno do servidor</response>
    [HttpGet("users/count")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserCountResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> GetUserCount()
    {
        var count = await _service.GetUserCountAsync();
        return Ok(new UserCountResponse { Users = count });
    }

    /// <summary>
    /// Obtém o tipo de licença de um usuário específico
    /// </summary>
    /// <param name="email">Email ou User Principal Name do usuário</param>
    /// <returns>Informações sobre a licença do usuário</returns>
    /// <response code="200">Retorna o tipo de licença do usuário</response>
    /// <response code="400">Email inválido</response>
    /// <response code="404">Usuário não encontrado</response>
    /// <response code="500">Erro interno do servidor</response>
    [HttpGet("users/license/{email}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserLicenseResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> GetUserLicense(string email)
    {
        var license = await _service.GetUserLicenseAsync(email);
        return Ok(new UserLicenseResponse { User = email, License = license });
    }

    /// <summary>
    /// Obtém os descritores dos administradores de um projeto
    /// </summary>
    /// <param name="projectId">ID ou nome do projeto</param>
    /// <returns>Lista de descritores dos administradores</returns>
    /// <response code="200">Retorna a lista de descritores</response>
    /// <response code="400">ID de projeto inválido</response>
    /// <response code="404">Projeto não encontrado</response>
    /// <response code="500">Erro interno do servidor</response>
    [HttpGet("projects/{projectId}/admins")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ProjectAdminsResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> GetProjectAdministrators(string projectId)
    {
        var admins = await _service.GetProjectAdministratorsAsync(projectId);
        return Ok(new ProjectAdminsResponse { Project = projectId, Administrators = admins });
    }

    /// <summary>
    /// Obtém os nomes resolvidos dos administradores de um projeto
    /// </summary>
    /// <param name="projectId">ID ou nome do projeto</param>
    /// <returns>Lista de nomes dos administradores</returns>
    /// <response code="200">Retorna a lista de administradores com nomes resolvidos</response>
    /// <response code="400">ID de projeto inválido</response>
    /// <response code="404">Projeto não encontrado</response>
    /// <response code="500">Erro interno do servidor</response>
    [HttpGet("projects/{projectId}/admins/resolved")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ProjectAdminsResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> GetProjectAdministratorsResolved(string projectId)
    {
        var admins = await _service.GetProjectAdministratorsResolvedAsync(projectId);
        return Ok(new ProjectAdminsResponse { Project = projectId, Administrators = admins });
    }

    /// <summary>
    /// Consulta o audit log do Azure DevOps para um período específico
    /// </summary>
    /// <param name="startTime">Data/hora inicial da consulta (formato ISO 8601)</param>
    /// <param name="endTime">Data/hora final da consulta (formato ISO 8601)</param>
    /// <param name="batchSize">Número de registros por página (padrão: 100, máximo: 5000)</param>
    /// <param name="continuationToken">Token para paginação (obtido de uma resposta anterior)</param>
    /// <returns>Registros do audit log</returns>
    /// <response code="200">Retorna os registros de auditoria</response>
    /// <response code="400">Parâmetros inválidos</response>
    /// <response code="500">Erro interno do servidor</response>
    /// <remarks>
    /// Exemplo de requisição:
    /// 
    ///     GET /api/audit/logs?startTime=2024-01-01T00:00:00Z&amp;endTime=2024-01-02T00:00:00Z&amp;batchSize=50
    ///     
    /// Observações:
    /// - O período máximo entre startTime e endTime é de 7 dias
    /// - As datas devem estar no formato ISO 8601 (UTC)
    /// - Use o continuationToken para buscar as próximas páginas quando hasMore=true
    /// </remarks>
    [HttpGet("logs")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AzureDevOpsAuditAgent.Class.AuditLogResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        [FromQuery] int batchSize = 100,
        [FromQuery] string? continuationToken = null)
    {
        // Validações
        if (!startTime.HasValue)
        {
            startTime = DateTime.UtcNow.AddDays(-1);
        }

        if (!endTime.HasValue)
        {
            endTime = DateTime.UtcNow;
        }

        if (startTime > endTime)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parâmetros inválidos",
                Detail = "A data inicial (startTime) não pode ser maior que a data final (endTime)",
                Status = StatusCodes.Status400BadRequest
            });
        }

        // Azure DevOps limita o período de consulta a 7 dias
        if ((endTime.Value - startTime.Value).TotalDays > 7)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Período muito longo",
                Detail = "O período máximo entre startTime e endTime é de 7 dias",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (batchSize < 1 || batchSize > 5000)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "BatchSize inválido",
                Detail = "O batchSize deve estar entre 1 e 5000",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await _service.GetAuditLogAsync(
            startTime.Value,
            endTime.Value,
            batchSize,
            continuationToken);

        return Ok(result);
    }

    #region Endpoints de Gerenciamento de Usuários e Grupos

    /// <summary>
    /// Lista todos os usuários da organização
    /// </summary>
    /// <returns>Lista completa de usuários</returns>
    /// <response code="200">Retorna a lista de usuários</response>
    /// <response code="500">Erro interno do servidor</response>
    /// <remarks>
    /// Retorna todos os usuários cadastrados na organização do Azure DevOps,
    /// incluindo seus descriptors (necessários para operações de adicionar/remover de grupos).
    /// </remarks>
    [HttpGet("users")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UsersListResponse))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _service.GetUsersAsync();
        return Ok(new UsersListResponse { Users = users, TotalCount = users.Count });
    }

    /// <summary>
    /// Busca um usuário específico por email
    /// </summary>
    /// <param name="email">Email ou UPN do usuário</param>
    /// <returns>Dados do usuário</returns>
    /// <response code="200">Retorna os dados do usuário</response>
    /// <response code="404">Usuário não encontrado</response>
    /// <response code="500">Erro interno do servidor</response>
    [HttpGet("users/search/{email}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AzureDevOpsAuditAgent.Class.GraphUser))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> GetUserByEmail(string email)
    {
        var user = await _service.GetUserByEmailAsync(email);

        if (user == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Usuário não encontrado",
                Detail = $"Não foi encontrado nenhum usuário com o email '{email}'",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(user);
    }

    /// <summary>
    /// Lista todos os grupos da organização
    /// </summary>
    /// <returns>Lista completa de grupos</returns>
    /// <response code="200">Retorna a lista de grupos</response>
    /// <response code="500">Erro interno do servidor</response>
    /// <remarks>
    /// Retorna todos os grupos da organização do Azure DevOps,
    /// incluindo grupos de projeto, equipes e grupos personalizados.
    /// </remarks>
    [HttpGet("groups")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GroupsListResponse))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> GetGroups()
    {
        var groups = await _service.GetGroupsAsync();
        return Ok(new GroupsListResponse { Groups = groups, TotalCount = groups.Count });
    }

    /// <summary>
    /// Busca um grupo específico por nome
    /// </summary>
    /// <param name="groupName">Nome ou display name do grupo</param>
    /// <returns>Dados do grupo</returns>
    /// <response code="200">Retorna os dados do grupo</response>
    /// <response code="404">Grupo não encontrado</response>
    /// <response code="500">Erro interno do servidor</response>
    [HttpGet("groups/search/{groupName}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AzureDevOpsAuditAgent.Class.GraphGroup))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> GetGroupByName(string groupName)
    {
        var group = await _service.GetGroupByNameAsync(groupName);

        if (group == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Grupo não encontrado",
                Detail = $"Não foi encontrado nenhum grupo com o nome '{groupName}'",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(group);
    }

    /// <summary>
    /// Lista os membros de um grupo específico
    /// </summary>
    /// <param name="groupDescriptor">Descriptor do grupo</param>
    /// <returns>Lista de membros do grupo</returns>
    /// <response code="200">Retorna a lista de membros</response>
    /// <response code="400">Descriptor inválido</response>
    /// <response code="404">Grupo não encontrado</response>
    /// <response code="500">Erro interno do servidor</response>
    [HttpGet("groups/{groupDescriptor}/members")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GroupMembersResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> GetGroupMembers(string groupDescriptor)
    {
        if (string.IsNullOrWhiteSpace(groupDescriptor))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Descriptor inválido",
                Detail = "O descriptor do grupo é obrigatório",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var members = await _service.GetGroupMembersAsync(groupDescriptor);
        return Ok(new GroupMembersResponse
        {
            GroupDescriptor = groupDescriptor,
            Members = members,
            TotalCount = members.Count
        });
    }

    /// <summary>
    /// Adiciona um usuário a um grupo
    /// </summary>
    /// <param name="groupDescriptor">Descriptor do grupo</param>
    /// <param name="userDescriptor">Descriptor do usuário</param>
    /// <returns>Resultado da operação</returns>
    /// <response code="200">Usuário adicionado com sucesso</response>
    /// <response code="400">Descriptors inválidos</response>
    /// <response code="404">Usuário ou grupo não encontrado</response>
    /// <response code="500">Erro interno do servidor</response>
    /// <remarks>
    /// Exemplo de requisição:
    /// 
    ///     PUT /api/audit/groups/vssgp.Uy0xLTktMTU...ABC/members/aad.MWY3YTFjZmQt...XYZ
    ///     
    /// Observações:
    /// - Os descriptors podem ser obtidos através dos endpoints /api/audit/users e /api/audit/groups
    /// - O PAT deve ter permissões de 'Graph' (Read &amp; Manage)
    /// - Se o usuário já for membro do grupo, a operação será bem-sucedida sem efeitos colaterais
    /// </remarks>
    [HttpPut("groups/{groupDescriptor}/members/{userDescriptor}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GroupOperationResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> AddUserToGroup(string groupDescriptor, string userDescriptor)
    {
        if (string.IsNullOrWhiteSpace(groupDescriptor))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Descriptor inválido",
                Detail = "O descriptor do grupo é obrigatório",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (string.IsNullOrWhiteSpace(userDescriptor))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Descriptor inválido",
                Detail = "O descriptor do usuário é obrigatório",
                Status = StatusCodes.Status400BadRequest
            });
        }

        await _service.AddUserToGroupAsync(groupDescriptor, userDescriptor);

        return Ok(new GroupOperationResponse
        {
            Success = true,
            Message = "Usuário adicionado ao grupo com sucesso",
            GroupDescriptor = groupDescriptor,
            UserDescriptor = userDescriptor
        });
    }

    /// <summary>
    /// Remove um usuário de um grupo
    /// </summary>
    /// <param name="groupDescriptor">Descriptor do grupo</param>
    /// <param name="userDescriptor">Descriptor do usuário</param>
    /// <returns>Resultado da operação</returns>
    /// <response code="200">Usuário removido com sucesso</response>
    /// <response code="400">Descriptors inválidos</response>
    /// <response code="404">Usuário ou grupo não encontrado</response>
    /// <response code="500">Erro interno do servidor</response>
    /// <remarks>
    /// Exemplo de requisição:
    /// 
    ///     DELETE /api/audit/groups/vssgp.Uy0xLTktMTU...ABC/members/aad.MWY3YTFjZmQt...XYZ
    ///     
    /// Observações:
    /// - Os descriptors podem ser obtidos através dos endpoints /api/audit/users e /api/audit/groups
    /// - O PAT deve ter permissões de 'Graph' (Read &amp; Manage)
    /// - Se o usuário não for membro do grupo, a operação retornará erro
    /// </remarks>
    [HttpDelete("groups/{groupDescriptor}/members/{userDescriptor}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GroupOperationResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> RemoveUserFromGroup(string groupDescriptor, string userDescriptor)
    {
        if (string.IsNullOrWhiteSpace(groupDescriptor))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Descriptor inválido",
                Detail = "O descriptor do grupo é obrigatório",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (string.IsNullOrWhiteSpace(userDescriptor))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Descriptor inválido",
                Detail = "O descriptor do usuário é obrigatório",
                Status = StatusCodes.Status400BadRequest
            });
        }

        await _service.RemoveUserFromGroupAsync(groupDescriptor, userDescriptor);

        return Ok(new GroupOperationResponse
        {
            Success = true,
            Message = "Usuário removido do grupo com sucesso",
            GroupDescriptor = groupDescriptor,
            UserDescriptor = userDescriptor
        });
    }

    #endregion
}

// DTOs para enriquecer o Swagger

/// <summary>
/// Resposta com a contagem de projetos
/// </summary>
public class ProjectCountResponse
{
    /// <summary>
    /// Número total de projetos na organização
    /// </summary>
    /// <example>15</example>
    public required int Projects { get; set; }
}

/// <summary>
/// Resposta com a contagem de usuários
/// </summary>
public class UserCountResponse
{
    /// <summary>
    /// Número total de usuários cadastrados
    /// </summary>
    /// <example>42</example>
    public required int Users { get; set; }
}

/// <summary>
/// Resposta com informações de licença do usuário
/// </summary>
public class UserLicenseResponse
{
    /// <summary>
    /// Email ou User Principal Name do usuário
    /// </summary>
    /// <example>usuario@exemplo.com</example>
    public required string User { get; set; }

    /// <summary>
    /// Tipo de licença do usuário
    /// </summary>
    /// <example>Visual Studio Enterprise</example>
    public required string License { get; set; }
}

/// <summary>
/// Resposta com a lista de administradores do projeto
/// </summary>
public class ProjectAdminsResponse
{
    /// <summary>
    /// ID ou nome do projeto
    /// </summary>
    /// <example>MeuProjeto</example>
    public required string Project { get; set; }

    /// <summary>
    /// Lista de administradores (descritores ou nomes)
    /// </summary>
    /// <example>["admin@exemplo.com", "gerente@exemplo.com"]</example>
    public required IEnumerable<string> Administrators { get; set; }
}

/// <summary>
/// Resposta com a lista de usuários
/// </summary>
public class UsersListResponse
{
    /// <summary>
    /// Lista de usuários
    /// </summary>
    public required List<AzureDevOpsAuditAgent.Class.GraphUser> Users { get; set; }

    /// <summary>
    /// Número total de usuários
    /// </summary>
    public int TotalCount { get; set; }
}

/// <summary>
/// Resposta com a lista de grupos
/// </summary>
public class GroupsListResponse
{
    /// <summary>
    /// Lista de grupos
    /// </summary>
    public required List<AzureDevOpsAuditAgent.Class.GraphGroup> Groups { get; set; }

    /// <summary>
    /// Número total de grupos
    /// </summary>
    public int TotalCount { get; set; }
}

/// <summary>
/// Resposta com a lista de membros de um grupo
/// </summary>
public class GroupMembersResponse
{
    /// <summary>
    /// Descriptor do grupo
    /// </summary>
    public required string GroupDescriptor { get; set; }

    /// <summary>
    /// Lista de membros
    /// </summary>
    public required List<AzureDevOpsAuditAgent.Class.GraphMember> Members { get; set; }

    /// <summary>
    /// Número total de membros
    /// </summary>
    public int TotalCount { get; set; }
}

/// <summary>
/// Resposta de operações em grupos (adicionar/remover membros)
/// </summary>
public class GroupOperationResponse
{
    /// <summary>
    /// Indica se a operação foi bem-sucedida
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Mensagem descritiva do resultado
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Descriptor do grupo
    /// </summary>
    public required string GroupDescriptor { get; set; }

    /// <summary>
    /// Descriptor do usuário
    /// </summary>
    public required string UserDescriptor { get; set; }
}