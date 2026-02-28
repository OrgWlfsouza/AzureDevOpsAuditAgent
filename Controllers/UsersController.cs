using AzureDevOpsAuditAgent.Class;
using AzureDevOpsAuditAgent.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureDevOpsAuditAgent.Controllers
{
    /// <summary>
    /// Controller para operações relacionadas a usuários do Azure DevOps
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class UsersController : ControllerBase
    {
        private readonly AzureDevOpsService _service;

        public UsersController(AzureDevOpsService service)
        {
            _service = service;
        }

        /// <summary>
        /// Obtém a contagem total de usuários cadastrados na organização
        /// </summary>
        /// <returns>Número total de usuários</returns>
        /// <response code="200">Retorna a contagem de usuários</response>
        /// <response code="500">Erro interno do servidor</response>
        [HttpGet("count")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserCountResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
        public async Task<IActionResult> GetUserCount()
        {
            var count = await _service.GetUserCountAsync();
            return Ok(new UserCountResponse { Users = count });
        }

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
        [HttpGet]
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
        [HttpGet("search/{email}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GraphUser))]
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
        /// Obtém o tipo de licença de um usuário específico
        /// </summary>
        /// <param name="email">Email ou User Principal Name do usuário</param>
        /// <returns>Informações sobre a licença do usuário</returns>
        /// <response code="200">Retorna o tipo de licença do usuário</response>
        /// <response code="400">Email inválido</response>
        /// <response code="404">Usuário não encontrado</response>
        /// <response code="500">Erro interno do servidor</response>
        [HttpGet("{email}/license")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserLicenseResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
        public async Task<IActionResult> GetUserLicense(string email)
        {
            var license = await _service.GetUserLicenseAsync(email);
            return Ok(new UserLicenseResponse { User = email, License = license });
        }
    }
}