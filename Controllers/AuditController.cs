using AzureDevOpsAuditAgent.Class;
using Microsoft.AspNetCore.Mvc;

namespace AzureDevOpsAuditAgent.Controllers
{
    /// <summary>
    /// Controller para operações de auditoria do Azure DevOps
    /// </summary>
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
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuditLogResponse))]
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
    }
}