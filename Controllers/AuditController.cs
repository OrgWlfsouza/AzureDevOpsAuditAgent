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
    public int Projects { get; set; }
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
    public int Users { get; set; }
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
    public string User { get; set; }

    /// <summary>
    /// Tipo de licença do usuário
    /// </summary>
    /// <example>Visual Studio Enterprise</example>
    public string License { get; set; }
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
    public string Project { get; set; }

    /// <summary>
    /// Lista de administradores (descritores ou nomes)
    /// </summary>
    /// <example>["admin@exemplo.com", "gerente@exemplo.com"]</example>
    public IEnumerable<string> Administrators { get; set; }
}