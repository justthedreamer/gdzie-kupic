using Hangfire.Dashboard;

namespace Gdzie.Kupic.Hangfire;

internal sealed class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}
