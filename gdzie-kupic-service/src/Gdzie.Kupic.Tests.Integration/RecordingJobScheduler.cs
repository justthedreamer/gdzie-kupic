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

    public IReadOnlyCollection<Type> Enqueued => _history.ToArray();

    public int CountOf<TJob>() => _history.Count(t => t == typeof(TJob));

    public string Enqueue<TJob>(Expression<Func<TJob, Task>> job)
    {
        _pending.Enqueue((typeof(TJob), job));
        _history.Enqueue(typeof(TJob));
        return Guid.NewGuid().ToString();
    }

    public string Schedule<TJob>(Expression<Func<TJob, Task>> job, TimeSpan delay) => Enqueue(job);

    public void AddOrUpdateRecurring<TJob>(string recurringJobId, Expression<Func<TJob, Task>> job, string cronExpression)
    {
    }

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

public sealed class RecordingNotificationDispatcher : Gdzie.Kupic.Notifications.INotificationDispatcher
{
    private readonly ConcurrentQueue<(Guid PostId, Guid MerchantId)> _dispatched = new();

    public IReadOnlyCollection<(Guid PostId, Guid MerchantId)> Dispatched => _dispatched.ToArray();

    public Task DispatchAsync(Guid postId, Guid merchantId, CancellationToken ct = default)
    {
        _dispatched.Enqueue((postId, merchantId));
        return Task.CompletedTask;
    }

    public void Reset() => _dispatched.Clear();
}
