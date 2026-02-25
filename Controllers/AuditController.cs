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
    public async Task<IActionResult> GetProjectCount()
    {
        var count = await _service.GetProjectCountAsync();
        return Ok(new { Projects = count });
    }

    [HttpGet("users/count")]
    public async Task<IActionResult> GetUserCount()
    {
        var count = await _service.GetUserCountAsync();
        return Ok(new { Users = count });
    }

    [HttpGet("users/license/{email}")]
    public async Task<IActionResult> GetUserLicense(string email)
    {
        var license = await _service.GetUserLicenseAsync(email);
        return Ok(new { User = email, License = license });
    }
}

