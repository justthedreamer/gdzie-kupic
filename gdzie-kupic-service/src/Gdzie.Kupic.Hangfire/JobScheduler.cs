using System.Linq.Expressions;
using Hangfire;

namespace Gdzie.Kupic.Hangfire;

internal sealed class JobScheduler(IBackgroundJobClient client, IRecurringJobManager recurringJobs) : IJobScheduler
{
    public string Enqueue<TJob>(Expression<Func<TJob, Task>> job) => client.Enqueue(job);

    public string Schedule<TJob>(Expression<Func<TJob, Task>> job, TimeSpan delay) => client.Schedule(job, delay);

    public void AddOrUpdateRecurring<TJob>(string recurringJobId, Expression<Func<TJob, Task>> job, string cronExpression) =>
        recurringJobs.AddOrUpdate(recurringJobId, job, cronExpression);

    public void RemoveRecurring(string recurringJobId) => recurringJobs.RemoveIfExists(recurringJobId);
}
