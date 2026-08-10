namespace VoidNote.Audio.Intelligence;

public interface IAiResourceGate
{
    Task<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default);
}

/// <summary>Prevents uncontrolled parallel model instantiation.</summary>
public sealed class AiResourceGate : IAiResourceGate, IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    public AiResourceGate(int maximumParallelJobs)
    {
        if (maximumParallelJobs <= 0) throw new ArgumentOutOfRangeException(nameof(maximumParallelJobs));
        _semaphore = new(maximumParallelJobs, maximumParallelJobs);
    }
    public async Task<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken); return new Lease(_semaphore);
    }
    public void Dispose() => _semaphore.Dispose();
    private sealed class Lease(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        private SemaphoreSlim? _semaphore = semaphore;
        public ValueTask DisposeAsync() { Interlocked.Exchange(ref _semaphore, null)?.Release(); return ValueTask.CompletedTask; }
    }
}
