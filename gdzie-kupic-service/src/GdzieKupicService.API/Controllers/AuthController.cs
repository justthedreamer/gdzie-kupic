namespace GdzieKupicService.API.Controllers;

using GdzieKupicService.API.Contracts;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/b/auth")]
public class AuthController : ControllerBase
{
    [HttpPost("u/login")]
    public async Task<ActionResult<UserContracts.Login.Response>> Login(
        [FromBody] UserContracts.Login.Request request)
    {
        return this.NoContent();
    }

    [HttpPost("u/register")]
    public async Task<ActionResult<UserContracts.Register.Response>> Register(
        [FromBody] UserContracts.Register.Request request)
    {
        return this.NoContent();
    }
    
    [HttpPost("m/login")]
    public async Task<ActionResult<MerchantContracts.Login.Response>> LoginUser(
        [FromBody] UserContracts.Login.Request request)
    {
        return this.NoContent();
    }

    [HttpPost("m/register")]
    public async Task<ActionResult<MerchantContracts.Register.Response>> RegisterUser(
        [FromBody] UserContracts.Register.Request request)
    {
        return this.NoContent();
    }
    
    [HttpPost("g/register")]
    public async Task<ActionResult<GuestContracts.Register.Response>> Register(
        [FromBody] GuestContracts.Register.Request request)
    {
        return this.NoContent();
    }
}