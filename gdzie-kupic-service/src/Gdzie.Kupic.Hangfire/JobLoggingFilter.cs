using System.Diagnostics;
using Hangfire.Client;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.States;
using Microsoft.Extensions.Logging;

namespace Gdzie.Kupic.Hangfire;

/// <summary>
/// Logs every job execution with <c>JobId</c>, <c>PostId</c> (when the job takes a
/// <c>postId</c> argument) and <c>CorrelationId</c> as structured properties. The correlation id
/// is captured when the job is created, so it follows the job across retries.
/// </summary>
internal sealed class JobLoggingFilter(ILoggerFactory loggerFactory)
    : JobFilterAttribute, IClientFilter, IServerFilter, IElectStateFilter
{
    internal const string CorrelationIdParameter = "CorrelationId";
    private const string ScopeItemKey = "gk.logging-scope";

    public void OnCreating(CreatingContext context)
    {
        if (context.GetJobParameter<string>(CorrelationIdParameter) is null)
        {
            context.SetJobParameter(
                CorrelationIdParameter,
                Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"));
        }
    }

    public void OnCreated(CreatedContext context)
    {
    }

    public void OnPerforming(PerformingContext context)
    {
        var logger = CreateLogger(context.BackgroundJob.Job);
        var scope = logger.BeginScope(Describe(
            context.BackgroundJob.Id,
            context.BackgroundJob.Job,
            context.GetJobParameter<string>(CorrelationIdParameter)));

        context.Items[ScopeItemKey] = scope;
        logger.LogInformation("Job {JobName} started", JobName(context.BackgroundJob.Job));
    }

    public void OnPerformed(PerformedContext context)
    {
        var logger = CreateLogger(context.BackgroundJob.Job);

        if (context.Exception is null)
        {
            logger.LogInformation("Job {JobName} succeeded", JobName(context.BackgroundJob.Job));
        }

        if (context.Items.TryGetValue(ScopeItemKey, out var scope))
        {
            (scope as IDisposable)?.Dispose();
        }
    }

    public void OnStateElection(ElectStateContext context)
    {
        if (context.CandidateState is not FailedState failed)
        {
            return;
        }

        var logger = CreateLogger(context.BackgroundJob.Job);

        using (logger.BeginScope(Describe(
                   context.BackgroundJob.Id,
                   context.BackgroundJob.Job,
                   context.GetJobParameter<string>(CorrelationIdParameter))))
        {
            logger.LogError(
                failed.Exception,
                "Job {JobName} failed (retries so far: {RetryCount})",
                JobName(context.BackgroundJob.Job),
                context.GetJobParameter<int>("RetryCount"));
        }
    }

    internal static Dictionary<string, object?> Describe(string? jobId, Job? job, string? correlationId)
    {
        var properties = new Dictionary<string, object?>
        {
            ["JobId"] = jobId,
            ["CorrelationId"] = correlationId,
        };

        if (job is not null)
        {
            var parameters = job.Method.GetParameters();

            for (var i = 0; i < parameters.Length && i < job.Args.Count; i++)
            {
                if (string.Equals(parameters[i].Name, "postId", StringComparison.OrdinalIgnoreCase)
                    && job.Args[i] is Guid postId)
                {
                    properties["PostId"] = postId;
                    break;
                }
            }
        }

        return properties;
    }

    private ILogger CreateLogger(Job? job) =>
        loggerFactory.CreateLogger(job?.Type.FullName ?? "Gdzie.Kupic.Hangfire");

    private static string JobName(Job? job) =>
        job is null ? "unknown" : $"{job.Type.Name}.{job.Method.Name}";
}
