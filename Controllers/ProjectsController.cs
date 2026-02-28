using AzureDevOpsAuditAgent.Class;
using AzureDevOpsAuditAgent.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureDevOpsAuditAgent.Controllers
{
    /// <summary>
    /// Controller para operações relacionadas a projetos do Azure DevOps
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ProjectsController : ControllerBase
    {
        private readonly AzureDevOpsService _service;

        public ProjectsController(AzureDevOpsService service)
        {
            _service = service;
        }

        /// <summary>
        /// Obtém a contagem total de projetos na organização Azure DevOps
        /// </summary>
        /// <returns>Número total de projetos</returns>
        /// <response code="200">Retorna a contagem de projetos</response>
        /// <response code="500">Erro interno do servidor</response>
        [HttpGet("count")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ProjectCountResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
        public async Task<IActionResult> GetProjectCount()
        {
            var count = await _service.GetProjectCountAsync();
            return Ok(new ProjectCountResponse { Projects = count });
        }

        /// <summary>
        /// Lista todos os projetos da organização Azure DevOps
        /// </summary>
        /// <param name="stateFilter">Filtro de estado: 'all', 'wellFormed', 'createPending', 'deleting', 'new' ou 'unchanged' (padrão: wellFormed)</param>
        /// <param name="top">Número máximo de projetos a retornar</param>
        /// <param name="skip">Número de projetos a pular (para paginação)</param>
        /// <returns>Lista de projetos</returns>
        /// <response code="200">Retorna a lista de projetos</response>
        /// <response code="400">Parâmetros inválidos</response>
        /// <response code="500">Erro interno do servidor</response>
        /// <remarks>
        /// Exemplo de requisição:
        /// 
        ///     GET /api/projects?stateFilter=wellFormed&amp;top=10&amp;skip=0
        ///     
        /// Observações:
        /// - Use stateFilter='all' para retornar todos os projetos independente do estado
        /// - Use top e skip para implementar paginação
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ProjectsResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
        public async Task<IActionResult> GetProjects(
            [FromQuery] string stateFilter = "wellFormed",
            [FromQuery] int? top = null,
            [FromQuery] int? skip = null)
        {
            // Validar stateFilter
            var validStates = new[] { "all", "wellFormed", "createPending", "deleting", "new", "unchanged" };
            if (!validStates.Contains(stateFilter, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "StateFilter inválido",
                    Detail = $"O stateFilter deve ser um dos seguintes valores: {string.Join(", ", validStates)}",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (top.HasValue && (top.Value < 1 || top.Value > 5000))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Parâmetro 'top' inválido",
                    Detail = "O valor de 'top' deve estar entre 1 e 5000",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (skip.HasValue && skip.Value < 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Parâmetro 'skip' inválido",
                    Detail = "O valor de 'skip' não pode ser negativo",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var result = await _service.GetProjectsAsync(stateFilter, top, skip);
            return Ok(result);
        }

        /// <summary>
        /// Obtém detalhes completos de um projeto específico
        /// </summary>
        /// <param name="projectIdOrName">ID (GUID) ou nome do projeto</param>
        /// <param name="includeCapabilities">Incluir informações de capacidades do projeto</param>
        /// <param name="includeHistory">Incluir histórico do projeto</param>
        /// <returns>Detalhes completos do projeto</returns>
        /// <response code="200">Retorna os detalhes do projeto</response>
        /// <response code="400">Parâmetro inválido</response>
        /// <response code="404">Projeto não encontrado</response>
        /// <response code="500">Erro interno do servidor</response>
        /// <remarks>
        /// Exemplo de requisição:
        /// 
        ///     GET /api/projects/MeuProjeto/details?includeCapabilities=true
        ///     
        /// Observações:
        /// - Pode-se usar o ID (GUID) ou o nome do projeto
        /// - includeCapabilities retorna informações sobre versionamento, processo, etc.
        /// </remarks>
        [HttpGet("{projectIdOrName}/details")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AzureDevOpsProjectDetails))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
        public async Task<IActionResult> GetProjectDetails(
            string projectIdOrName,
            [FromQuery] bool includeCapabilities = false,
            [FromQuery] bool includeHistory = false)
        {
            if (string.IsNullOrWhiteSpace(projectIdOrName))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Parâmetro inválido",
                    Detail = "O ID ou nome do projeto é obrigatório",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            try
            {
                var result = await _service.GetProjectDetailsAsync(projectIdOrName, includeCapabilities, includeHistory);
                return Ok(result);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Projeto não encontrado",
                    Detail = $"Não foi encontrado nenhum projeto com o identificador '{projectIdOrName}'",
                    Status = StatusCodes.Status404NotFound
                });
            }
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
        [HttpGet("{projectId}/administrators")]
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
        [HttpGet("{projectId}/administrators/resolved")]
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
}