namespace HKW.MVVM;

internal sealed class CombineLatestSubscription<TResult> : IDisposable
{
    private readonly Lock _gate = new();
    private readonly IObserver<TResult> _observer;
    private readonly Func<object?[], TResult> _selector;
    private readonly object?[] _values;
    private readonly bool[] _hasValues;
    private readonly bool[] _completed;
    private readonly MultipleDisposable _subscriptions = new();
    private bool _stopped;

    public CombineLatestSubscription(IObserver<TResult> observer, int sourceCount, Func<object?[], TResult> selector)
    {
        _observer = observer;
        _selector = selector;
        _values = new object?[sourceCount];
        _hasValues = new bool[sourceCount];
        _completed = new bool[sourceCount];
    }

    public bool IsStopped { get { lock (_gate) return _stopped; } }

    public void Subscribe<T>(int index, IObservable<T> source)
    {
        if (IsStopped) return;
        var slot = new SingleAssignmentDisposable();
        _subscriptions.Add(slot);
        try
        {
            slot.Disposable = source.Subscribe(
                value => Next(index, value),
                Fail,
                () => Complete(index, slot));
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private void Next<T>(int index, T value)
    {
        Exception? failure = null;
        lock (_gate)
        {
            if (_stopped) return;
            _values[index] = value;
            _hasValues[index] = true;
            if (!_hasValues.All(static hasValue => hasValue)) return;
            try
            {
                _observer.OnNext(_selector((object?[])_values.Clone()));
            }
            catch (Exception exception)
            {
                failure = exception;
                _stopped = true;
            }
        }
        if (failure is not null)
        {
            _observer.OnError(failure);
            _subscriptions.Dispose();
        }
    }

    private void Complete(int index, SingleAssignmentDisposable slot)
    {
        var complete = false;
        lock (_gate)
        {
            if (_stopped) return;
            _completed[index] = true;
            complete = !_hasValues[index] || _completed.All(static value => value);
            if (complete) _stopped = true;
        }
        _subscriptions.Remove(slot);
        slot.Dispose();
        if (complete)
        {
            _observer.OnCompleted();
            _subscriptions.Dispose();
        }
    }

    private void Fail(Exception error)
    {
        lock (_gate)
        {
            if (_stopped) return;
            _stopped = true;
        }
        _observer.OnError(error);
        _subscriptions.Dispose();
    }

    public void Dispose()
    {
        lock (_gate) _stopped = true;
        _subscriptions.Dispose();
    }
}

internal sealed class MergeSubscription<T> : IDisposable
{
    private readonly Lock _gate = new();
    private readonly IObserver<T> _observer;
    private readonly MultipleDisposable _subscriptions = new();
    private int _active;
    private bool _enumerationComplete;
    private bool _stopped;

    public MergeSubscription(IObserver<T> observer) => _observer = observer;

    public void Run(IEnumerable<IObservable<T>> sources)
    {
        IEnumerator<IObservable<T>>? enumerator = null;
        try
        {
            enumerator = sources.GetEnumerator();
            while (true)
            {
                bool moved;
                IObservable<T>? source;
                try
                {
                    moved = enumerator.MoveNext();
                    source = moved ? enumerator.Current : null;
                }
                catch (Exception exception)
                {
                    Fail(exception);
                    return;
                }
                if (!moved) break;
                if (source is null)
                {
                    Fail(new ArgumentNullException(nameof(sources), "The source sequence contains a null observable."));
                    return;
                }
                if (!SubscribeInner(source)) return;
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
            return;
        }
        finally
        {
            try { enumerator?.Dispose(); }
            catch (Exception exception) { Fail(exception); }
        }

        var complete = false;
        lock (_gate)
        {
            _enumerationComplete = true;
            complete = !_stopped && _active == 0;
            if (complete) _stopped = true;
        }
        if (complete) _observer.OnCompleted();
    }

    private bool SubscribeInner(IObservable<T> source)
    {
        lock (_gate)
        {
            if (_stopped) return false;
            _active++;
        }
        var slot = new SingleAssignmentDisposable();
        _subscriptions.Add(slot);
        try
        {
            slot.Disposable = source.Subscribe(Next, Fail, () => InnerCompleted(slot));
            lock (_gate) return !_stopped;
        }
        catch (Exception exception)
        {
            Fail(exception);
            return false;
        }
    }

    private void Next(T value)
    {
        lock (_gate)
        {
            if (_stopped) return;
            _observer.OnNext(value);
        }
    }

    private void InnerCompleted(SingleAssignmentDisposable slot)
    {
        var complete = false;
        lock (_gate)
        {
            if (_stopped) return;
            _active--;
            complete = _enumerationComplete && _active == 0;
            if (complete) _stopped = true;
        }
        _subscriptions.Remove(slot);
        slot.Dispose();
        if (complete) _observer.OnCompleted();
    }

    public void Fail(Exception error)
    {
        lock (_gate)
        {
            if (_stopped) return;
            _stopped = true;
        }
        _observer.OnError(error);
        _subscriptions.Dispose();
    }

    public void Dispose()
    {
        lock (_gate) _stopped = true;
        _subscriptions.Dispose();
    }
}

internal sealed class SelectManySubscription<TSource, TResult> : IDisposable
{
    private readonly Lock _gate = new();
    private readonly IObserver<TResult> _observer;
    private readonly Func<TSource, IObservable<TResult>> _selector;
    private readonly MultipleDisposable _subscriptions = new();
    private int _active = 1;
    private bool _stopped;

    public SelectManySubscription(IObserver<TResult> observer, Func<TSource, IObservable<TResult>> selector)
    {
        _observer = observer;
        _selector = selector;
    }

    public void Subscribe(IObservable<TSource> source)
    {
        var slot = new SingleAssignmentDisposable();
        _subscriptions.Add(slot);
        try { slot.Disposable = source.Subscribe(OuterNext, Fail, () => OuterCompleted(slot)); }
        catch (Exception exception) { Fail(exception); }
    }

    private void OuterNext(TSource value)
    {
        lock (_gate) { if (_stopped) return; }
        IObservable<TResult> inner;
        try { inner = _selector(value) ?? throw new InvalidOperationException("The selector returned a null observable."); }
        catch (Exception exception) { Fail(exception); return; }

        lock (_gate)
        {
            if (_stopped) return;
            _active++;
        }
        var slot = new SingleAssignmentDisposable();
        _subscriptions.Add(slot);
        try { slot.Disposable = inner.Subscribe(Next, Fail, () => Completed(slot)); }
        catch (Exception exception) { Fail(exception); }
    }

    private void OuterCompleted(SingleAssignmentDisposable slot) => Completed(slot);

    private void Completed(SingleAssignmentDisposable? slot)
    {
        var complete = false;
        lock (_gate)
        {
            if (_stopped) return;
            _active--;
            complete = _active == 0;
            if (complete) _stopped = true;
        }
        if (slot is not null)
        {
            _subscriptions.Remove(slot);
            slot.Dispose();
        }
        if (complete)
        {
            _observer.OnCompleted();
            _subscriptions.Dispose();
        }
    }

    private void Next(TResult value)
    {
        lock (_gate)
        {
            if (!_stopped) _observer.OnNext(value);
        }
    }

    private void Fail(Exception error)
    {
        lock (_gate)
        {
            if (_stopped) return;
            _stopped = true;
        }
        _observer.OnError(error);
        _subscriptions.Dispose();
    }

    public void Dispose()
    {
        lock (_gate) _stopped = true;
        _subscriptions.Dispose();
    }
}

internal sealed class SwitchSubscription<T> : IDisposable
{
    private readonly Lock _gate = new();
    private readonly IObserver<T> _observer;
    private readonly MultipleDisposable _subscriptions = new();
    private SingleAssignmentDisposable? _currentInner;
    private long _version;
    private bool _outerCompleted;
    private bool _hasInner;
    private bool _stopped;

    public SwitchSubscription(IObserver<T> observer) => _observer = observer;

    public void Subscribe(IObservable<IObservable<T>> source)
    {
        var slot = new SingleAssignmentDisposable();
        _subscriptions.Add(slot);
        try { slot.Disposable = source.Subscribe(OuterNext, Fail, () => OuterComplete(slot)); }
        catch (Exception exception) { Fail(exception); }
    }

    private void OuterNext(IObservable<T> source)
    {
        if (source is null) { Fail(new InvalidOperationException("The outer sequence produced a null observable.")); return; }
        long version;
        SingleAssignmentDisposable? previous;
        var slot = new SingleAssignmentDisposable();
        _subscriptions.Add(slot);
        lock (_gate)
        {
            if (_stopped)
            {
                _subscriptions.Remove(slot);
                slot.Dispose();
                return;
            }
            version = ++_version;
            _hasInner = true;
            previous = _currentInner;
            _currentInner = slot;
        }

        if (previous is not null)
        {
            _subscriptions.Remove(previous);
            previous.Dispose();
        }

        try
        {
            slot.Disposable = source.Subscribe(
                value => Next(version, value),
                error => InnerFail(version, error),
                () => InnerComplete(version, slot));
        }
        catch (Exception exception) { InnerFail(version, exception); }
    }

    private void Next(long version, T value)
    {
        lock (_gate)
        {
            if (!_stopped && version == _version) _observer.OnNext(value);
        }
    }

    private void InnerFail(long version, Exception error)
    {
        lock (_gate) { if (_stopped || version != _version) return; }
        Fail(error);
    }

    private void InnerComplete(long version, SingleAssignmentDisposable slot)
    {
        var complete = false;
        lock (_gate)
        {
            if (_stopped || version != _version || !ReferenceEquals(_currentInner, slot)) return;
            _currentInner = null;
            _hasInner = false;
            complete = _outerCompleted;
            if (complete) _stopped = true;
        }
        _subscriptions.Remove(slot);
        slot.Dispose();
        if (complete) { _observer.OnCompleted(); _subscriptions.Dispose(); }
    }

    private void OuterComplete(SingleAssignmentDisposable slot)
    {
        var complete = false;
        lock (_gate)
        {
            if (_stopped) return;
            _outerCompleted = true;
            complete = !_hasInner;
            if (complete) _stopped = true;
        }
        _subscriptions.Remove(slot);
        slot.Dispose();
        if (complete) { _observer.OnCompleted(); _subscriptions.Dispose(); }
    }

    private void Fail(Exception error)
    {
        lock (_gate)
        {
            if (_stopped) return;
            _stopped = true;
        }
        _observer.OnError(error);
        _subscriptions.Dispose();
    }

    public void Dispose()
    {
        lock (_gate) _stopped = true;
        _subscriptions.Dispose();
    }
}

internal sealed class ConcatSubscription<T> : IDisposable
{
    private readonly Lock _gate = new();
    private readonly IObserver<T> _observer;
    private readonly IEnumerable<IObservable<T>> _sources;
    private readonly SerialDisposable _current = new();
    private IEnumerator<IObservable<T>>? _enumerator;
    private int _draining;
    private bool _stopped;

    public ConcatSubscription(IObserver<T> observer, IEnumerable<IObservable<T>> sources)
    {
        _observer = observer;
        _sources = sources;
    }

    public void Run()
    {
        try { _enumerator = _sources.GetEnumerator(); }
        catch (Exception exception) { Fail(exception); return; }
        Drain();
    }

    private void Drain()
    {
        if (Interlocked.Increment(ref _draining) != 1) return;
        do
        {
            if (IsStopped) continue;
            bool moved;
            IObservable<T>? source;
            try
            {
                moved = _enumerator!.MoveNext();
                source = moved ? _enumerator.Current : null;
            }
            catch (Exception exception) { Fail(exception); continue; }
            if (!moved) { Complete(); continue; }
            if (source is null) { Fail(new ArgumentNullException(nameof(_sources), "The source sequence contains a null observable.")); continue; }
            var slot = new SingleAssignmentDisposable();
            _current.Disposable = slot;
            try { slot.Disposable = source.Subscribe(Next, Fail, Drain); }
            catch (Exception exception) { Fail(exception); }
        } while (Interlocked.Decrement(ref _draining) != 0);
    }

    private bool IsStopped { get { lock (_gate) return _stopped; } }

    private void Next(T value)
    {
        lock (_gate) { if (!_stopped) _observer.OnNext(value); }
    }

    private void Complete()
    {
        lock (_gate)
        {
            if (_stopped) return;
            _stopped = true;
        }
        try { _enumerator?.Dispose(); }
        catch (Exception exception) { _observer.OnError(exception); _current.Dispose(); return; }
        _observer.OnCompleted();
        _current.Dispose();
    }

    private void Fail(Exception error)
    {
        lock (_gate)
        {
            if (_stopped) return;
            _stopped = true;
        }
        try { _enumerator?.Dispose(); } catch { }
        _observer.OnError(error);
        _current.Dispose();
    }

    public void Dispose()
    {
        lock (_gate) _stopped = true;
        try { _enumerator?.Dispose(); } catch { }
        _current.Dispose();
    }
}
