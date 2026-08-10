using System.Collections.Concurrent;

namespace VoidNote.Application.Jobs;

public enum BackgroundJobState { Queued, Running, Completed, Cancelled, Failed }

public sealed record JobProgress(double Fraction, string Message)
{
    public static JobProgress Indeterminate(string message) => new(-1, message);
}

public sealed class BackgroundJob
{
    internal BackgroundJob(string name, CancellationTokenSource cancellation)
    { Id = Guid.NewGuid(); Name = name; Cancellation = cancellation; }
    public Guid Id { get; }
    public string Name { get; }
    public BackgroundJobState State { get; internal set; } = BackgroundJobState.Queued;
    public JobProgress Progress { get; internal set; } = JobProgress.Indeterminate("Waiting");
    public Exception? Error { get; internal set; }
    internal CancellationTokenSource Cancellation { get; }
    public void Cancel() => Cancellation.Cancel();
}

public interface IBackgroundJobManager
{
    IReadOnlyCollection<BackgroundJob> Jobs { get; }
    event EventHandler<BackgroundJob>? JobChanged;
    Task<T> RunAsync<T>(string name, Func<IProgress<JobProgress>, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
    Task CancelAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>Runs cancellable work off the UI context and preserves observable status.</summary>
public sealed class BackgroundJobManager : IBackgroundJobManager
{
    private readonly ConcurrentDictionary<Guid, BackgroundJob> _jobs = new();
    public IReadOnlyCollection<BackgroundJob> Jobs => _jobs.Values.OrderBy(job => job.Name).ToArray();
    public event EventHandler<BackgroundJob>? JobChanged;

    public async Task<T> RunAsync<T>(string name, Func<IProgress<JobProgress>, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name); ArgumentNullException.ThrowIfNull(operation);
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var job = new BackgroundJob(name, linked); _jobs[job.Id] = job; Notify(job);
        var progress = new Progress<JobProgress>(value => { job.Progress = value; Notify(job); });
        try
        {
            job.State = BackgroundJobState.Running; Notify(job);
            var result = await Task.Run(() => operation(progress, linked.Token), linked.Token);
            job.Progress = new JobProgress(1, "Complete"); job.State = BackgroundJobState.Completed; Notify(job);
            return result;
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        { job.State = BackgroundJobState.Cancelled; Notify(job); throw; }
        catch (Exception exception)
        { job.Error = exception; job.State = BackgroundJobState.Failed; Notify(job); throw; }
        finally { linked.Dispose(); }
    }

    public async Task CancelAllAsync(CancellationToken cancellationToken = default)
    {
        var running = _jobs.Values.Where(job => job.State is BackgroundJobState.Queued or BackgroundJobState.Running).ToArray();
        foreach (var job in running) job.Cancel();
        while (running.Any(job => job.State is BackgroundJobState.Queued or BackgroundJobState.Running))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(25, cancellationToken).ConfigureAwait(false);
        }
    }

    private void Notify(BackgroundJob job) => JobChanged?.Invoke(this, job);
}
