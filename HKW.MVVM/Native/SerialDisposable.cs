namespace HKW.MVVM;

internal sealed class SerialDisposable : IDisposable
{
    private readonly
#if NET10_0_OR_GREATER
    Lock
#else
    object
#endif
    _gate = new();
    private IDisposable? _current;
    private bool _disposed;

    public IDisposable? Disposable
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
        set
        {
            IDisposable? previous;
            lock (_gate)
            {
                if (_disposed)
                {
                    previous = value;
                }
                else
                {
                    previous = _current;
                    _current = value;
                }
            }

            previous?.Dispose();
        }
    }

    public bool IsDisposed
    {
        get
        {
            lock (_gate)
            {
                return _disposed;
            }
        }
    }

    public void Dispose()
    {
        IDisposable? current;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            current = _current;
            _current = null;
        }

        current?.Dispose();
    }
}
