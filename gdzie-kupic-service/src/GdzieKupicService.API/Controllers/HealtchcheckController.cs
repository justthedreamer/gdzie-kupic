namespace GdzieKupicService.API.Controllers;

using Microsoft.AspNetCore.Mvc;

[Route("api/health")]
public class HealthCheckController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => this.Ok("Service is running");
}