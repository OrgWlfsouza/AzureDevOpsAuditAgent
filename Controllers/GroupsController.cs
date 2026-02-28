using AzureDevOpsAuditAgent.Class;
using AzureDevOpsAuditAgent.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureDevOpsAuditAgent.Controllers
{
    /// <summary>
    /// Controller para operações relacionadas a grupos do Azure DevOps
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class GroupsController : ControllerBase
    {
        private readonly AzureDevOpsService _service;

        public GroupsController(AzureDevOpsService service)
        {
            _service = service;
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
        [HttpGet]
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
        [HttpGet("search/{groupName}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GraphGroup))]
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
        [HttpGet("{groupDescriptor}/members")]
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
        ///     PUT /api/groups/vssgp.Uy0xLTktMTU...ABC/members/aad.MWY3YTFjZmQt...XYZ
        ///     
        /// Observações:
        /// - Os descriptors podem ser obtidos através dos endpoints /api/users e /api/groups
        /// - O PAT deve ter permissões de 'Graph' (Read &amp; Manage)
        /// - Se o usuário já for membro do grupo, a operação será bem-sucedida sem efeitos colaterais
        /// </remarks>
        [HttpPut("{groupDescriptor}/members/{userDescriptor}")]
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
        ///     DELETE /api/groups/vssgp.Uy0xLTktMTU...ABC/members/aad.MWY3YTFjZmQt...XYZ
        ///     
        /// Observações:
        /// - Os descriptors podem ser obtidos através dos endpoints /api/users e /api/groups
        /// - O PAT deve ter permissões de 'Graph' (Read &amp; Manage)
        /// - Se o usuário não for membro do grupo, a operação retornará erro
        /// </remarks>
        [HttpDelete("{groupDescriptor}/members/{userDescriptor}")]
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
    }
}