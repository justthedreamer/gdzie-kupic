namespace Gdzie.Kupic.Hangfire;

/// <summary>
/// Ambient correlation id of the code that enqueues a job; it is stored with the job and
/// restored in the logs of every execution. Set by the API request pipeline.
/// </summary>
public static class JobCorrelation
{
    private static readonly AsyncLocal<string?> Value = new();

    public static string? Current
    {
        get => Value.Value;
        set => Value.Value = value;
    }
}
