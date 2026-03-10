using Microsoft.AspNetCore.Mvc;
using AzureDevOpsAuditAgent.Attributes;
using AzureDevOpsAuditAgent.Class;

namespace AzureDevOpsAuditAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkItemsController : ControllerBase
{
    private readonly AzureDevOpsService _azureDevOpsService;
    private readonly ILogger<WorkItemsController> _logger;

    public WorkItemsController(
        AzureDevOpsService azureDevOpsService,
        ILogger<WorkItemsController> logger)
    {
        _azureDevOpsService = azureDevOpsService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a Work Item by ID
    /// </summary>
    /// <param name="workItemId">Work Item ID</param>
    /// <returns>Work Item details</returns>
    [HttpGet("{workItemId}")]
    [ProducesResponseType(typeof(WorkItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkItem(int workItemId)
    {
        try
        {
            var workItem = await _azureDevOpsService.GetWorkItemAsync(workItemId);
            return Ok(workItem);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error getting Work Item {WorkItemId}", workItemId);
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Creates a new Work Item
    /// </summary>
    /// <param name="request">Work Item data to create</param>
    /// <returns>Created Work Item</returns>
    [HttpPost]
    [FeatureFlag("WorkItemCreation")]
    [ProducesResponseType(typeof(WorkItem), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateWorkItem([FromBody] CreateWorkItemRequest request)
    {
        try
        {
            var workItem = await _azureDevOpsService.CreateWorkItemAsync(
                request.ProjectIdOrName,
                request.WorkItemType,
                request.Fields);

            return CreatedAtAction(
                nameof(GetWorkItem),
                new { workItemId = workItem.Id },
                workItem);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("feature"))
        {
            // Feature flag disabled
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "Operation temporarily disabled",
                details = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Work Item");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing Work Item
    /// </summary>
    /// <param name="workItemId">Work Item ID</param>
    /// <param name="fields">Fields to update</param>
    /// <returns>Updated Work Item</returns>
    [HttpPatch("{workItemId}")]
    [FeatureFlag("WorkItemUpdate")]
    [ProducesResponseType(typeof(WorkItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UpdateWorkItem(
        int workItemId,
        [FromBody] Dictionary<string, object> fields)
    {
        try
        {
            var workItem = await _azureDevOpsService.UpdateWorkItemAsync(workItemId, fields);
            return Ok(workItem);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("feature"))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "Operation temporarily disabled",
                details = ex.Message
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error updating Work Item {WorkItemId}", workItemId);
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a Work Item
    /// </summary>
    /// <param name="workItemId">Work Item ID</param>
    /// <param name="destroy">If true, deletes permanently</param>
    /// <returns>Deletion confirmation</returns>
    [HttpDelete("{workItemId}")]
    [FeatureFlag("WorkItemDeletion")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteWorkItem(
        int workItemId,
        [FromQuery] bool destroy = false)
    {
        try
        {
            await _azureDevOpsService.DeleteWorkItemAsync(workItemId, destroy);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("feature"))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "Operation temporarily disabled",
                details = ex.Message
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error deleting Work Item {WorkItemId}", workItemId);
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Executes a WIQL query to search for Work Items
    /// </summary>
    /// <param name="projectIdOrName">Project ID or name</param>
    /// <param name="wiql">WIQL query</param>
    /// <returns>Query result</returns>
    [HttpPost("query")]
    [FeatureFlag("WorkItemQuery")]
    [ProducesResponseType(typeof(WorkItemQueryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> QueryWorkItems(
        [FromQuery] string projectIdOrName,
        [FromBody] string wiql)
    {
        try
        {
            var result = await _azureDevOpsService.QueryWorkItemsAsync(projectIdOrName, wiql);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("feature"))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "Operation temporarily disabled",
                details = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing WIQL query");
            return BadRequest(new { error = ex.Message });
        }
    }
}

// DTO for Work Item creation
public class CreateWorkItemRequest
{
    public required string ProjectIdOrName { get; set; }
    public required string WorkItemType { get; set; }
    public required Dictionary<string, object> Fields { get; set; }
}