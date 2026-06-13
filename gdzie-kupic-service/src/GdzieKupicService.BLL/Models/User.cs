namespace GdzieKupicService.Core.Models;

internal class User
{
    public Guid Id { get; internal set; }
    public string Name { get; internal set; }
    public string Email { get; set; }
    public string Password { get; set; }
}