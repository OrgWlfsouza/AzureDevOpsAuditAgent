using AzureDevOpsAuditAgent.Class;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly AzureDevOpsService _service;

    public AuditController(AzureDevOpsService service)
    {
        _service = service;
    }

    [HttpGet("projects/count")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ProjectCountResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProjectCount()
    {
        var count = await _service.GetProjectCountAsync();
        return Ok(new ProjectCountResponse { Projects = count });
    }

    [HttpGet("users/count")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserCountResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUserCount()
    {
        var count = await _service.GetUserCountAsync();
        return Ok(new UserCountResponse { Users = count });
    }

    [HttpGet("users/license/{email}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserLicenseResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUserLicense(string email)
    {
        var license = await _service.GetUserLicenseAsync(email);
        return Ok(new UserLicenseResponse { User = email, License = license });
    }

    [HttpGet("projects/{projectId}/admins")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ProjectAdminsResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProjectAdministrators(string projectId)
    {
        var admins = await _service.GetProjectAdministratorsAsync(projectId);
        return Ok(new ProjectAdminsResponse { Project = projectId, Administrators = admins });
    }

    [HttpGet("projects/{projectId}/admins/resolved")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ProjectAdminsResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProjectAdministratorsResolved(string projectId)
    {
        var admins = await _service.GetProjectAdministratorsResolvedAsync(projectId);
        return Ok(new ProjectAdminsResponse { Project = projectId, Administrators = admins });
    }
}

// DTOs para enriquecer o Swagger
public class ProjectCountResponse
{
    public int Projects { get; set; }
}

public class UserCountResponse
{
    public int Users { get; set; }
}

public class UserLicenseResponse
{
    public string User { get; set; }
    public string License { get; set; }
}

public class ProjectAdminsResponse
{
    public string Project { get; set; }
    public IEnumerable<string> Administrators { get; set; }
}