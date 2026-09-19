using System.Threading;

namespace QuickLaunch.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _mutex;
    private bool _owns;
    public SingleInstanceService(string name) { _mutex = new Mutex(true, name, out _owns); }
    public bool TryAcquire() => _owns;
    public void Dispose() { if (_owns) _mutex.ReleaseMutex(); _mutex.Dispose(); }
}
