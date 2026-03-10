using Microsoft.AspNetCore.Mvc;
using AzureDevOpsAuditAgent.Attributes;
using AzureDevOpsAuditAgent.Class;

namespace AzureDevOpsAuditAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserGroupsController : ControllerBase
{
    private readonly AzureDevOpsService _azureDevOpsService;
    private readonly ILogger<UserGroupsController> _logger;

    public UserGroupsController(
        AzureDevOpsService azureDevOpsService,
        ILogger<UserGroupsController> logger)
    {
        _azureDevOpsService = azureDevOpsService;
        _logger = logger;
    }

    /// <summary>
    /// Lists all users in the organization
    /// </summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(List<GraphUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUsers()
    {
        try
        {
            var users = await _azureDevOpsService.GetUsersAsync();
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing users");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Lists all groups in the organization
    /// </summary>
    [HttpGet("groups")]
    [ProducesResponseType(typeof(List<GraphGroup>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetGroups()
    {
        try
        {
            var groups = await _azureDevOpsService.GetGroupsAsync();
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing groups");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Searches for a specific group by name
    /// </summary>
    [HttpGet("search/{groupName}")]
    [ProducesResponseType(typeof(GraphGroup), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SearchGroup(string groupName)
    {
        try
        {
            var group = await _azureDevOpsService.GetGroupByNameAsync(groupName);
            if (group == null)
            {
                return NotFound(new { error = $"Group '{groupName}' not found" });
            }
            return Ok(group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching group");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Lists members of a specific group
    /// </summary>
    [HttpGet("{groupDescriptor}/members")]
    [ProducesResponseType(typeof(List<GraphMember>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetGroupMembers(string groupDescriptor)
    {
        try
        {
            var members = await _azureDevOpsService.GetGroupMembersAsync(groupDescriptor);
            return Ok(members);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing group members");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Adds a user to a group
    /// </summary>
    [HttpPut("{groupDescriptor}/members/{userDescriptor}")]
    [FeatureFlag("UserGroupManagement")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> AddUserToGroup(
        string groupDescriptor,
        string userDescriptor)
    {
        try
        {
            await _azureDevOpsService.AddUserToGroupAsync(groupDescriptor, userDescriptor);
            _logger.LogInformation("User {UserDescriptor} added to group {GroupDescriptor}", userDescriptor, groupDescriptor);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("feature") || ex.Message.Contains("blocked"))
        {
            _logger.LogWarning(ex, "Feature flag blocked operation");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "Operation temporarily disabled",
                details = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user to group");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Removes a user from a group
    /// </summary>
    [HttpDelete("{groupDescriptor}/members/{userDescriptor}")]
    [FeatureFlag("UserGroupManagement")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RemoveUserFromGroup(
        string groupDescriptor,
        string userDescriptor)
    {
        try
        {
            await _azureDevOpsService.RemoveUserFromGroupAsync(groupDescriptor, userDescriptor);
            _logger.LogInformation("User {UserDescriptor} removed from group {GroupDescriptor}", userDescriptor, groupDescriptor);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("feature") || ex.Message.Contains("blocked"))
        {
            _logger.LogWarning(ex, "Feature flag blocked operation");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "Operation temporarily disabled",
                details = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user from group");
            return BadRequest(new { error = ex.Message });
        }
    }
}