namespace HKW.MVVMTest;

internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _utcNow;
    private long _timestamp;
    private long _nextSequence;

    public ManualTimeProvider()
        : this(DateTimeOffset.UnixEpoch) { }

    public ManualTimeProvider(DateTimeOffset start)
    {
        _utcNow = start.ToUniversalTime();
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _utcNow;
        }
    }

    public override long GetTimestamp()
    {
        lock (_gate)
        {
            return _timestamp;
        }
    }

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period
    )
    {
        ArgumentNullException.ThrowIfNull(callback);
        ValidateTimeout(dueTime, nameof(dueTime));
        ValidateTimeout(period, nameof(period));

        ManualTimer timer;
        lock (_gate)
        {
            timer = new ManualTimer(this, callback, state, ++_nextSequence);
            _timers.Add(timer);
            timer.ChangeUnderLock(dueTime, period);
        }

        return timer;
    }

    public void Advance(TimeSpan amount)
    {
        if (amount < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        DateTimeOffset target;
        lock (_gate)
        {
            target = _utcNow.Add(amount);
        }

        while (true)
        {
            ManualTimer? timer;
            lock (_gate)
            {
                timer = _timers
                    .Where(candidate => candidate.IsDueUnderLock(target))
                    .OrderBy(candidate => candidate.DueAt)
                    .ThenBy(candidate => candidate.Sequence)
                    .FirstOrDefault();
                if (timer is null)
                {
                    MoveClockUnderLock(target);
                    return;
                }

                MoveClockUnderLock(timer.DueAt);
                timer.PrepareForCallbackUnderLock();
            }

            timer.InvokeCallback();
        }
    }

    private void MoveClockUnderLock(DateTimeOffset value)
    {
        var elapsed = value - _utcNow;
        _utcNow = value;
        _timestamp = checked(_timestamp + elapsed.Ticks);
    }

    private static void ValidateTimeout(TimeSpan value, string parameterName)
    {
        if (value < Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private sealed class ManualTimer(
        ManualTimeProvider owner,
        TimerCallback callback,
        object? state,
        long sequence
    ) : ITimer
    {
        private readonly ManualTimeProvider _owner = owner;
        private readonly TimerCallback _callback = callback;
        private readonly object? _state = state;
        private bool _disposed;
        private TimeSpan _period;

        public DateTimeOffset DueAt { get; private set; } = DateTimeOffset.MaxValue;

        public long Sequence { get; } = sequence;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            ValidateTimeout(dueTime, nameof(dueTime));
            ValidateTimeout(period, nameof(period));
            lock (_owner._gate)
            {
                if (_disposed)
                {
                    return false;
                }

                ChangeUnderLock(dueTime, period);
                return true;
            }
        }

        public void Dispose()
        {
            lock (_owner._gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                DueAt = DateTimeOffset.MaxValue;
                _owner._timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        internal void ChangeUnderLock(TimeSpan dueTime, TimeSpan period)
        {
            _period = period;
            DueAt = dueTime == Timeout.InfiniteTimeSpan
                ? DateTimeOffset.MaxValue
                : _owner._utcNow.Add(dueTime);
        }

        internal bool IsDueUnderLock(DateTimeOffset target) =>
            !_disposed && DueAt != DateTimeOffset.MaxValue && DueAt <= target;

        internal void PrepareForCallbackUnderLock()
        {
            if (_period > TimeSpan.Zero)
            {
                DueAt = DueAt.Add(_period);
            }
            else
            {
                DueAt = DateTimeOffset.MaxValue;
            }
        }

        internal void InvokeCallback() => _callback(_state);
    }
}
