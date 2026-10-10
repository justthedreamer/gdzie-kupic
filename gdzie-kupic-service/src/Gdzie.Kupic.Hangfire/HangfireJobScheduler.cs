using System.Linq.Expressions;
using Hangfire;

namespace Gdzie.Kupic.Hangfire;

internal sealed class HangfireJobScheduler(
    IBackgroundJobClient backgroundJobs,
    IRecurringJobManager recurringJobs) : IJobScheduler
{
    public string Enqueue<TJob>(Expression<Func<TJob, Task>> job) =>
        backgroundJobs.Enqueue(job);

    public string Schedule<TJob>(Expression<Func<TJob, Task>> job, TimeSpan delay) =>
        backgroundJobs.Schedule(job, delay);

    public void AddOrUpdateRecurring<TJob>(string recurringJobId, Expression<Func<TJob, Task>> job, string cron) =>
        recurringJobs.AddOrUpdate(recurringJobId, job, cron);
}
