using System.Collections.Concurrent;
using System.Linq.Expressions;
using Gdzie.Kupic.Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace Gdzie.Kupic.Tests.Integration;

/// <summary>
/// Stands in for the Hangfire-backed scheduler: records what was enqueued so tests can inspect it
/// and run the queued jobs deterministically (including jobs enqueued by other jobs).
/// </summary>
public sealed class RecordingJobScheduler(IServiceProvider services) : IJobScheduler
{
    private readonly ConcurrentQueue<(Type JobType, LambdaExpression Expression)> _pending = new();
    private readonly ConcurrentQueue<Type> _history = new();

    private readonly ConcurrentDictionary<string, (string Cron, TimeZoneInfo? TimeZone)> _recurring = new();

    public IReadOnlyDictionary<string, (string Cron, TimeZoneInfo? TimeZone)> Recurring => _recurring;

    public IReadOnlyCollection<Type> Enqueued => _history.ToArray();

    public int CountOf<TJob>() => _history.Count(t => t == typeof(TJob));

    public string Enqueue<TJob>(Expression<Func<TJob, Task>> job)
    {
        _pending.Enqueue((typeof(TJob), job));
        _history.Enqueue(typeof(TJob));
        return Guid.NewGuid().ToString();
    }

    public string Schedule<TJob>(Expression<Func<TJob, Task>> job, TimeSpan delay) => Enqueue(job);

    public void AddOrUpdateRecurring<TJob>(
        string recurringJobId, Expression<Func<TJob, Task>> job, string cronExpression, TimeZoneInfo? timeZone = null) =>
        _recurring[recurringJobId] = (cronExpression, timeZone);

    public void RemoveRecurring(string recurringJobId)
    {
    }

    public void Reset()
    {
        _pending.Clear();
        _history.Clear();
    }

    public async Task RunPendingAsync()
    {
        while (_pending.TryDequeue(out var entry))
        {
            using var scope = services.CreateScope();
            var instance = scope.ServiceProvider.GetRequiredService(entry.JobType);
            await (Task)entry.Expression.Compile().DynamicInvoke(instance)!;
        }
    }
}

/// <summary>Log of every notification handed to the dispatcher; the real dispatcher still runs behind it.</summary>
public sealed class RecordingNotificationDispatcher
{
    private readonly ConcurrentQueue<Gdzie.Kupic.Notifications.Notification> _dispatched = new();

    public IReadOnlyCollection<Gdzie.Kupic.Notifications.Notification> Dispatched => _dispatched.ToArray();

    public void Record(Gdzie.Kupic.Notifications.Notification notification) => _dispatched.Enqueue(notification);

    public void Reset() => _dispatched.Clear();
}

internal sealed class RecordingDispatcherDecorator(
    Gdzie.Kupic.Notifications.NotificationDispatcher inner,
    RecordingNotificationDispatcher log) : Gdzie.Kupic.Notifications.INotificationDispatcher
{
    public Task DispatchAsync(Gdzie.Kupic.Notifications.Notification notification, CancellationToken ct = default)
    {
        log.Record(notification);
        return inner.DispatchAsync(notification, ct);
    }
}
