using Gdzie.Kupic.Domain.Model;

namespace Gdzie.Kupic.Domain.Services;

public interface IDomainMapper
{
    public (Role role, string? invalidRoleError) MapRole(string role);
}