using Microsoft.AspNetCore.Mvc;
using AzureDevOpsAuditAgent.Class;

namespace AzureDevOpsAuditAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PipelinesController : ControllerBase
{
    private readonly AzureDevOpsService _azureDevOpsService;
    private readonly ILogger<PipelinesController> _logger;

    public PipelinesController(
        AzureDevOpsService azureDevOpsService,
        ILogger<PipelinesController> logger)
    {
        _azureDevOpsService = azureDevOpsService;
        _logger = logger;
    }

    /// <summary>
    /// Lists all pipelines of a project
    /// </summary>
    /// <param name="project">Project name or ID</param>
    /// <returns>List of pipelines</returns>
    [HttpGet("{project}")]
    [ProducesResponseType(typeof(List<Pipeline>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPipelines(string project)
    {
        try
        {
            var pipelines = await _azureDevOpsService.GetPipelinesAsync(project);
            return Ok(pipelines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing pipelines for project {Project}", project);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets details of a specific pipeline
    /// </summary>
    /// <param name="project">Project name or ID</param>
    /// <param name="pipelineId">Pipeline ID</param>
    [HttpGet("{project}/{pipelineId}")]
    [ProducesResponseType(typeof(Pipeline), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPipeline(string project, int pipelineId)
    {
        try
        {
            var pipeline = await _azureDevOpsService.GetPipelineAsync(project, pipelineId);
            if (pipeline == null)
            {
                return NotFound(new { error = $"Pipeline {pipelineId} not found in project {project}" });
            }
            return Ok(pipeline);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pipeline {PipelineId} from project {Project}", pipelineId, project);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Lists all runs of a pipeline
    /// </summary>
    /// <param name="project">Project name or ID</param>
    /// <param name="pipelineId">Pipeline ID</param>
    [HttpGet("{project}/{pipelineId}/runs")]
    [ProducesResponseType(typeof(List<PipelineRun>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPipelineRuns(string project, int pipelineId)
    {
        try
        {
            var runs = await _azureDevOpsService.GetPipelineRunsAsync(project, pipelineId);
            return Ok(runs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing runs for pipeline {PipelineId}", pipelineId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Show me failed runs of pipeline between specific dates
    /// </summary>
    /// <param name="project">Project name or ID</param>
    /// <param name="pipelineId">Pipeline ID</param>
    /// <param name="startDate">Start date (format: yyyy-MM-dd)</param>
    /// <param name="endDate">End date (format: yyyy-MM-dd)</param>
    /// <example>
    /// GET /api/Pipelines/MyProject/123/failed-runs?startDate=2025-03-01&endDate=2025-03-09
    /// </example>
    [HttpGet("{project}/{pipelineId}/failed-runs")]
    [ProducesResponseType(typeof(List<PipelineRun>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFailedRuns(
        string project, 
        int pipelineId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        try
        {
            if (startDate > endDate)
            {
                return BadRequest(new { error = "Start date must be before end date" });
            }

            var failedRuns = await _azureDevOpsService.GetFailedPipelineRunsAsync(
                project, pipelineId, startDate, endDate);
            
            return Ok(new
            {
                project,
                pipelineId,
                dateRange = new { startDate, endDate },
                totalFailedRuns = failedRuns.Count,
                runs = failedRuns
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting failed runs for pipeline {PipelineId}", pipelineId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Lists all logs from a pipeline run
    /// </summary>
    /// <param name="project">Project name or ID</param>
    /// <param name="pipelineId">Pipeline ID</param>
    /// <param name="runId">Run ID</param>
    [HttpGet("{project}/{pipelineId}/runs/{runId}/logs")]
    [ProducesResponseType(typeof(LogCollection), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPipelineLogs(string project, int pipelineId, int runId)
    {
        try
        {
            var logs = await _azureDevOpsService.GetPipelineLogsAsync(project, pipelineId, runId);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting logs for run {RunId} of pipeline {PipelineId}", runId, pipelineId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets the content of a specific log
    /// </summary>
    /// <param name="project">Project name or ID</param>
    /// <param name="pipelineId">Pipeline ID</param>
    /// <param name="runId">Run ID</param>
    /// <param name="logId">Log ID</param>
    [HttpGet("{project}/{pipelineId}/runs/{runId}/logs/{logId}/content")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("text/plain")]
    public async Task<IActionResult> GetLogContent(string project, int pipelineId, int runId, int logId)
    {
        try
        {
            var content = await _azureDevOpsService.GetLogContentAsync(project, pipelineId, runId, logId);
            return Content(content, "text/plain");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting content of log {LogId}", logId);
            return BadRequest(new { error = ex.Message });
        }
    }
}