using Gdzie.Kupic.Domain.Model;

namespace Gdzie.Kupic.Domain.Services;

internal class DomainMapper : IDomainMapper
{
    public (Role role, string? invalidRoleError) MapRole(string role)
    {
        if (!Enum.TryParse<Role>(role, out var result))
        {
            return (default, $"Failed to parse role, given value: {role}, doesn't match any role.");
        }

        return (result, null);
    }
}