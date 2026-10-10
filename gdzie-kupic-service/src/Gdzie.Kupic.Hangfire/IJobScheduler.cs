using System.Linq.Expressions;

namespace Gdzie.Kupic.Hangfire;

/// <summary>
/// The only way other modules schedule background work. Jobs are described by a method call on
/// a job type resolved from DI when the job runs; arguments must be serializable.
/// </summary>
public interface IJobScheduler
{
    /// <summary>Runs the job as soon as a worker is free. Returns the job identifier.</summary>
    string Enqueue<TJob>(Expression<Func<TJob, Task>> job);

    /// <summary>Runs the job once after <paramref name="delay"/>. Returns the job identifier.</summary>
    string Schedule<TJob>(Expression<Func<TJob, Task>> job, TimeSpan delay);

    /// <summary>
    /// Creates or updates a recurring job. <paramref name="cron"/> is a cron expression; the
    /// minimum resolution is one minute.
    /// </summary>
    void AddOrUpdateRecurring<TJob>(string recurringJobId, Expression<Func<TJob, Task>> job, string cron);
}
