namespace ClaudeToolbar.App.Services;

/// <summary>First instance owns a named mutex and listens on a named event; later instances signal it and exit.</summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = "ClaudeToolbar.Instance";
    private const string EventName = "ClaudeToolbar.OpenSettings";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _event;
    private readonly RegisteredWaitHandle? _wait;

    private SingleInstance(Mutex mutex, EventWaitHandle evt, bool isFirst, Action? onSignal)
    {
        _mutex = mutex;
        _event = evt;
        IsFirst = isFirst;
        if (isFirst && onSignal is not null)
            _wait = ThreadPool.RegisterWaitForSingleObject(evt, (_, _) => onSignal(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public bool IsFirst { get; }

    public static SingleInstance Acquire(Action onOpenSettingsRequested)
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        var evt = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        if (!createdNew)
        {
            evt.Set();
            return new SingleInstance(mutex, evt, isFirst: false, onSignal: null);
        }
        return new SingleInstance(mutex, evt, isFirst: true, onOpenSettingsRequested);
    }

    public void Dispose()
    {
        _wait?.Unregister(null);
        if (IsFirst)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
        }
        _mutex.Dispose();
        _event.Dispose();
    }
}
