using AzureDevOpsAuditAgent.Class;
using Microsoft.AspNetCore.Mvc;

namespace AzureDevOpsAuditAgent.Controllers
{
    /// <summary>
    /// Controller para gerenciamento de Work Items do Azure DevOps
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class WorkItemsController : ControllerBase
    {
        private readonly AzureDevOpsService _service;
        private readonly ILogger<WorkItemsController> _logger;

        public WorkItemsController(AzureDevOpsService service, ILogger<WorkItemsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Cria um novo Work Item
        /// </summary>
        /// <param name="request">Dados do Work Item a criar</param>
        /// <returns>Work Item criado</returns>
        /// <response code="201">Work Item criado com sucesso</response>
        /// <response code="400">Requisição inválida</response>
        /// <response code="500">Erro interno do servidor</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(WorkItem))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
        public async Task<IActionResult> CreateWorkItem([FromBody] CreateWorkItemRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Project))
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Projeto obrigatório",
                        Detail = "O campo 'Project' é obrigatório.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }

                if (string.IsNullOrWhiteSpace(request.WorkItemType))
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Tipo de Work Item obrigatório",
                        Detail = "O campo 'WorkItemType' é obrigatório.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }

                if (request.Fields == null || !request.Fields.Any())
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Campos obrigatórios",
                        Detail = "É necessário fornecer pelo menos um campo para o Work Item.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }

                _logger.LogInformation(
                    "Criando Work Item do tipo {WorkItemType} no projeto {Project}",
                    request.WorkItemType,
                    request.Project);

                var workItem = await _service.CreateWorkItemAsync(
                    request.Project,
                    request.WorkItemType,
                    request.Fields);

                return CreatedAtAction(
                    nameof(GetWorkItem),
                    new { id = workItem.Id },
                    workItem);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro HTTP ao criar Work Item");
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Erro ao criar Work Item",
                    Detail = ex.Message,
                    Status = StatusCodes.Status500InternalServerError
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao criar Work Item");
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Erro interno do servidor",
                    Detail = ex.Message,
                    Status = StatusCodes.Status500InternalServerError
                });
            }
        }

        /// <summary>
        /// Obtém um Work Item por ID
        /// </summary>
        /// <param name="id">ID do Work Item</param>
        /// <param name="fields">Campos específicos a retornar (separados por vírgula)</param>
        /// <param name="expand">Opções de expansão: None, Relations, Fields, Links, All</param>
        /// <returns>Work Item encontrado</returns>
        /// <response code="200">Work Item encontrado</response>
        /// <response code="404">Work Item não encontrado</response>
        /// <response code="500">Erro interno do servidor</response>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkItem))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
        public async Task<IActionResult> GetWorkItem(
            int id,
            [FromQuery] string? fields = null,
            [FromQuery] string expand = "All")
        {
            try
            {
                _logger.LogInformation("Buscando Work Item {WorkItemId}", id);

                var fieldList = string.IsNullOrWhiteSpace(fields)
                    ? null
                    : fields.Split(',').Select(f => f.Trim()).ToList();

                var workItem = await _service.GetWorkItemAsync(id, fieldList, expand);

                return Ok(workItem);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                _logger.LogWarning("Work Item {WorkItemId} não encontrado", id);
                return NotFound(new ProblemDetails
                {
                    Title = "Work Item não encontrado",
                    Detail = $"O Work Item com ID {id} não foi encontrado.",
                    Status = StatusCodes.Status404NotFound
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro HTTP ao buscar Work Item {WorkItemId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Erro ao buscar Work Item",
                    Detail = ex.Message,
                    Status = StatusCodes.Status500InternalServerError
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao buscar Work Item {WorkItemId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Erro interno do servidor",
                    Detail = ex.Message,
                    Status = StatusCodes.Status500InternalServerError
                });
            }
        }

        /// <summary>
        /// Obtém múltiplos Work Items por IDs
        /// </summary>
        /// <param name="ids">Lista de IDs separados por vírgula</param>
        /// <param name="fields">Campos específicos a retornar (separados por vírgula)</param>
        /// <returns>Lista de Work Items encontrados</returns>
        /// <response code="200">Work Items encontrados</response>
        /// <response code="400">Requisição inválida</response>
        /// <response code="500">Erro interno do servidor</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<WorkItem>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
        public async Task<IActionResult> GetWorkItems(
            [FromQuery] string ids,
            [FromQuery] string? fields = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ids))
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "IDs obrigatórios",
                        Detail = "É necessário fornecer pelo menos um ID de Work Item.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }

                var idList = ids.Split(',')
                    .Select(id => int.TryParse(id.Trim(), out var result) ? result : (int?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToList();

                if (!idList.Any())
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "IDs inválidos",
                        Detail = "Nenhum ID válido foi fornecido.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }

                _logger.LogInformation("Buscando {Count} Work Items", idList.Count);

                var fieldList = string.IsNullOrWhiteSpace(fields)
                    ? null
                    : fields.Split(',').Select(f => f.Trim()).ToList();

                var workItems = await _service.GetWorkItemsAsync(idList, fieldList);

                return Ok(workItems);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro HTTP ao buscar Work Items");
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Erro ao buscar Work Items",
                    Detail = ex.Message,
                    Status = StatusCodes.Status500InternalServerError
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao buscar Work Items");
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Erro interno do servidor",
                    Detail = ex.Message,
                    Status = StatusCodes.Status500InternalServerError
                });
            }
        }

        /// <summary>
        /// Executa uma query WIQL para buscar Work Items
        /// </summary>
        /// <param name="request">Query WIQL a executar</param>
        /// <returns>Resultado da query com os Work Items encontrados</returns>
        /// <response code="200">Query executada com sucesso</response>
        /// <response code="400">Query inválida</response>
        /// <response code="500">Erro interno do servidor</response>
        [HttpPost("query")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkItemQueryResult))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
        public async Task<IActionResult> QueryWorkItems([FromBody] WorkItemQueryRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Project))
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Projeto obrigatório",
                        Detail = "O campo 'Project' é obrigatório.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Wiql))
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Query WIQL obrigatória",
                        Detail = "O campo 'Wiql' é obrigatório.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }

                _logger.LogInformation("Executando query WIQL no projeto {Project}", request.Project);

                var result = await _service.QueryWorkItemsAsync(request.Project, request.Wiql);

                return Ok(result);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro HTTP ao executar query WIQL");
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Erro ao executar query WIQL",
                    Detail = ex.Message,
                    Status = StatusCodes.Status500InternalServerError
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao executar query WIQL");
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Erro interno do servidor",
                    Detail = ex.Message,
                    Status = StatusCodes.Status500InternalServerError
                });
            }
        }

        /// <summary>
        /// Atualiza um Work Item existente
        /// </summary>
        /// <param name="id">ID do Work Item</param>
        /// <param name="request">Campos a atualizar</param>
        /// <returns>Work Item atualizado</returns>
        /// <response code="200">Work Item atualizado com sucesso</response>
        /// <response code="400">Requisição inválida</response>
        /// <response code="404">Work Item não encontrado</response>
        /// <response code="500">Erro interno do servidor</response>
        [HttpPatch("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WorkItem))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
        public async Task<IActionResult> UpdateWorkItem(int id, [FromBody] UpdateWorkItemRequest request)
        {
            try
            {
                if (request.Fields == null || !request.Fields.Any())
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Campos obrigatórios",
                        Detail = "É necessário fornecer pelo menos um campo para atualizar.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }

                _logger.LogInformation("Atualizando Work Item {WorkItemId}", id);

                var workItem = await _service.UpdateWorkItemAsync(id, request.Fields);

                return Ok(workItem);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                _logger.LogWarning("Work Item {WorkItemId} não encontrado", id);
                return NotFound(new ProblemDetails
                {
                    Title = "Work Item não encontrado",
                    Detail = $"O Work Item com ID {id} não foi encontrado.",
                    Status = StatusCodes.Status404NotFound
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro HTTP ao atualizar Work Item {WorkItemId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Erro ao atualizar Work Item",
                    Detail = ex.Message,
                    Status = StatusCodes.Status500InternalServerError
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao atualizar Work Item {WorkItemId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Erro interno do servidor",
                    Detail = ex.Message,
                    Status = StatusCodes.Status500InternalServerError
                });
            }
        }

        /// <summary>
        /// Deleta um Work Item
        /// </summary>
        /// <param name="id">ID do Work Item</param>
        /// <param name="destroy">Se true, deleta permanentemente; se false, move para a lixeira</param>
        /// <returns>Confirmação da exclusão</returns>
        /// <response code="204">Work Item deletado com sucesso</response>
        /// <response code="404">Work Item não encontrado</response>
        /// <response code="500">Erro interno do servidor</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
        public async Task<IActionResult> DeleteWorkItem(int id, [FromQuery] bool destroy = false)
        {
            try
            {
                _logger.LogInformation(
                    "Deletando Work Item {WorkItemId} (destroy: {Destroy})",
                    id,
                    destroy);

                await _service.DeleteWorkItemAsync(id, destroy);

                return NoContent();
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                _logger.LogWarning("Work Item {WorkItemId} não encontrado", id);
                return NotFound(new ProblemDetails
                {
                    Title = "Work Item não encontrado",
                    Detail = $"O Work Item com ID {id} não foi encontrado.",
                    Status = StatusCodes.Status404NotFound
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro HTTP ao deletar Work Item {WorkItemId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Erro ao deletar Work Item",
                    Detail = ex.Message,
                    Status = StatusCodes.Status500InternalServerError
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao deletar Work Item {WorkItemId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Erro interno do servidor",
                    Detail = ex.Message,
                    Status = StatusCodes.Status500InternalServerError
                });
            }
        }
    }

    #region Request Models

    /// <summary>
    /// Modelo de requisição para criar um Work Item
    /// </summary>
    public class CreateWorkItemRequest
    {
        /// <summary>
        /// Nome ou ID do projeto
        /// </summary>
        public required string Project { get; set; }

        /// <summary>
        /// Tipo do Work Item (Bug, Task, User Story, Feature, Epic, etc.)
        /// </summary>
        public required string WorkItemType { get; set; }

        /// <summary>
        /// Campos do Work Item (ex: System.Title, System.Description, System.State, etc.)
        /// </summary>
        public required Dictionary<string, object> Fields { get; set; }
    }

    /// <summary>
    /// Modelo de requisição para atualizar um Work Item
    /// </summary>
    public class UpdateWorkItemRequest
    {
        /// <summary>
        /// Campos a atualizar (ex: System.State, System.AssignedTo, etc.)
        /// </summary>
        public required Dictionary<string, object> Fields { get; set; }
    }

    /// <summary>
    /// Modelo de requisição para executar uma query WIQL
    /// </summary>
    public class WorkItemQueryRequest
    {
        /// <summary>
        /// Nome ou ID do projeto
        /// </summary>
        public required string Project { get; set; }

        /// <summary>
        /// Query WIQL (Work Item Query Language)
        /// </summary>
        public required string Wiql { get; set; }
    }

    #endregion
}