using System.Linq.Expressions;

namespace Gdzie.Kupic.Hangfire;

/// <summary>
/// The only way for other modules to schedule background work; hides the Hangfire runtime.
/// Jobs are resolved from DI. Job methods may take a parameter named <c>postId</c> which is
/// then included in the job execution logs.
/// </summary>
public interface IJobScheduler
{
    string Enqueue<TJob>(Expression<Func<TJob, Task>> job);

    string Schedule<TJob>(Expression<Func<TJob, Task>> job, TimeSpan delay);

    /// <param name="timeZone">The zone the cron expression is evaluated in; UTC when null.</param>
    void AddOrUpdateRecurring<TJob>(
        string recurringJobId, Expression<Func<TJob, Task>> job, string cronExpression, TimeZoneInfo? timeZone = null);

    void RemoveRecurring(string recurringJobId);
}
