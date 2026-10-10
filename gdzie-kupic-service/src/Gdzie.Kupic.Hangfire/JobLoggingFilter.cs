using System.Diagnostics;
using Hangfire.Client;
using Hangfire.Common;
using Hangfire.Server;
using Microsoft.Extensions.Logging;

namespace Gdzie.Kupic.Hangfire;

internal sealed class JobLoggingFilter(ILogger<JobLoggingFilter> logger) : JobFilterAttribute, IClientFilter, IServerFilter
{
    private const string CorrelationParameter = "CorrelationId";
    private const string ScopeKey = "JobLoggingScope";
    private const string StopwatchKey = "JobStopwatch";

    public void OnCreating(CreatingContext context)
    {
        var correlationId = JobCorrelation.Current ?? Activity.Current?.TraceId.ToString();
        if (correlationId is not null)
            context.SetJobParameter(CorrelationParameter, correlationId);
    }

    public void OnCreated(CreatedContext context)
    {
    }

    public void OnPerforming(PerformingContext context)
    {
        var jobId = context.BackgroundJob.Id;
        var postId = FindPostId(context);
        var correlationId = context.GetJobParameter<string>(CorrelationParameter);
        var retryCount = context.GetJobParameter<int>("RetryCount");

        context.Items[ScopeKey] = logger.BeginScope(new Dictionary<string, object?>
        {
            ["JobId"] = jobId,
            ["PostId"] = postId,
            ["CorrelationId"] = correlationId,
        });
        context.Items[StopwatchKey] = Stopwatch.StartNew();

        logger.LogInformation("Job {JobName} started (attempt {Attempt})", JobName(context), retryCount + 1);
    }

    public void OnPerformed(PerformedContext context)
    {
        var elapsed = (context.Items[StopwatchKey] as Stopwatch)?.ElapsedMilliseconds;

        if (context.Exception is null || context.ExceptionHandled)
            logger.LogInformation("Job {JobName} succeeded in {ElapsedMs} ms", JobName(context), elapsed);
        else
            logger.LogError(context.Exception, "Job {JobName} failed after {ElapsedMs} ms", JobName(context), elapsed);

        (context.Items[ScopeKey] as IDisposable)?.Dispose();
    }

    private static string JobName(PerformContext context) =>
        $"{context.BackgroundJob.Job.Type.Name}.{context.BackgroundJob.Job.Method.Name}";

    private static Guid? FindPostId(PerformContext context)
    {
        var parameters = context.BackgroundJob.Job.Method.GetParameters();
        for (var i = 0; i < parameters.Length && i < context.BackgroundJob.Job.Args.Count; i++)
        {
            if (!string.Equals(parameters[i].Name, "postId", StringComparison.OrdinalIgnoreCase))
                continue;

            return context.BackgroundJob.Job.Args[i] switch
            {
                Guid guid => guid,
                string text when Guid.TryParse(text, out var parsed) => parsed,
                _ => null,
            };
        }

        return null;
    }
}
