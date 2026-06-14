namespace GdzieKupicService.API.Controllers;

using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
public class HealthCheckController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => this.Ok("Gdzie Kupic Service is running.");
}