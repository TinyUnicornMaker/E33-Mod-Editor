namespace E33Packer.App;

/// <summary>
/// Process-wide lock so two generator windows cannot race over work/mod,
/// the donor cache, or the installed seed trio.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = "ExpeditionInfinity.SeedGenerator.1.3";

    private readonly Mutex _mutex;
    private bool _owned;

    private SingleInstance(Mutex mutex, bool owned)
    {
        _mutex = mutex;
        _owned = owned;
    }

    /// <summary>Returns null when another generator process owns the lock.</summary>
    public static SingleInstance? TryAcquire()
    {
        try
        {
            var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            if (createdNew) return new SingleInstance(mutex, owned: true);
            mutex.Dispose();
            return null;
        }
        catch
        {
            // A platform without named mutex support must still allow the app
            // to start; the build-stage lock inside the app remains.
            return new SingleInstance(new Mutex(), owned: false);
        }
    }

    public void Dispose()
    {
        if (_owned)
        {
            try { _mutex.ReleaseMutex(); }
            catch { /* already released */ }
            _owned = false;
        }
        _mutex.Dispose();
    }
}
