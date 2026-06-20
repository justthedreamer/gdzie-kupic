namespace Gdzie.Kupic.Service.API.Controllers;

using Microsoft.AspNetCore.Mvc;

[Route("api/health")]
public class HealthCheckController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => this.Ok("Service is running");
    
    [HttpPost("api/check/{id:guid}")]
    public Task Post(Guid id) => Task.CompletedTask;
}