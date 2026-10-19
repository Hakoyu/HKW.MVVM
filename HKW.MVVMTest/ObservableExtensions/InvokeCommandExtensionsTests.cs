using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HKW.MVVM;

namespace HKW.MVVMTest;

[TestClass]
public sealed class InvokeCommandExtensionsTests
{
    [TestMethod]
    public void InvokeCommand_ChecksEveryValueAndExecutesOnlyAllowedValues()
    {
        var source = new ManualObservable<int>();
        var command = new RecordingCommand(value => (int)value! % 2 == 0);
        using var subscription = source.InvokeCommand(command);

        source.Emit(1);
        source.Emit(2);
        source.Emit(4);

        CollectionAssert.AreEqual(new object?[] { 1, 2, 4 }, command.CheckedValues);
        CollectionAssert.AreEqual(new object?[] { 2, 4 }, command.ExecutedValues);
    }

    [TestMethod]
    public void InvokeCommand_PropertyOverloadTracksReplacementAndDropsValuesWhileNull()
    {
        var source = new ManualObservable<string>();
        var first = new RecordingCommand();
        var second = new RecordingCommand();
        var target = new CommandOwner { Command = first };
        using var subscription = source.InvokeCommand(target, owner => owner.Command);

        source.Emit("first");
        target.Command = null;
        source.Emit("ignored");
        target.Command = second;
        source.Emit("second");

        CollectionAssert.AreEqual(new object?[] { "first" }, first.ExecutedValues);
        CollectionAssert.AreEqual(new object?[] { "second" }, second.ExecutedValues);
    }

    [TestMethod]
    public void InvokeCommand_PropertyReplacementDuringCanExecuteUsesCheckedCommand()
    {
        var source = new ManualObservable<int>();
        var replacement = new RecordingCommand();
        var target = new CommandOwner();
        var original = new RecordingCommand(
            _ =>
            {
                target.Command = replacement;
                return true;
            }
        );
        target.Command = original;
        using var subscription = source.InvokeCommand(target, owner => owner.Command);

        source.Emit(1);
        source.Emit(2);

        CollectionAssert.AreEqual(new object?[] { 1 }, original.ExecutedValues);
        CollectionAssert.AreEqual(new object?[] { 2 }, replacement.ExecutedValues);
    }

    [TestMethod]
    public void InvokeCommand_WhenCanExecuteThrows_PublishesOnceAndStops()
    {
        var source = new ManualObservable<int>();
        var expected = new TestException("CanExecute failed.");
        var command = new RecordingCommand(_ => throw expected);
        using var result = source.InvokeCommand(command);
        var errors = new List<Exception>();
        result.ThrownExceptions.Subscribe(errors.Add);

        source.Emit(1);
        source.Emit(2);

        CollectionAssert.AreEqual(new[] { expected }, errors);
        Assert.AreEqual(1, source.DisposalCount);
        Assert.AreEqual(1, command.CheckedValues.Count);
    }

    [TestMethod]
    public void InvokeCommand_WhenExecuteThrowsSynchronously_ReplaysErrorWithoutThrowing()
    {
        var expected = new TestException("Execute failed.");
        var command = new RecordingCommand(execute: _ => throw expected);
        var source = new SynchronousObservable<int>(observer =>
        {
            observer.OnNext(1);
            observer.OnNext(2);
        });

        using var result = source.InvokeCommand(command);
        var errors = new List<Exception>();
        result.ThrownExceptions.Subscribe(errors.Add);

        CollectionAssert.AreEqual(new[] { expected }, errors);
        Assert.AreEqual(1, source.DisposalCount);
        Assert.AreEqual(1, command.ExecutedValues.Count);
    }

    [TestMethod]
    public void InvokeCommand_WhenSourceCompletesSynchronously_DisposesSourceSubscription()
    {
        var command = new RecordingCommand();
        var source = new SynchronousObservable<int>(observer =>
        {
            observer.OnNext(1);
            observer.OnCompleted();
            observer.OnNext(2);
        });

        using var subscription = source.InvokeCommand(command);

        CollectionAssert.AreEqual(new object?[] { 1 }, command.ExecutedValues);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void InvokeCommand_TypedRelayCommandUsesTypedValue()
    {
        var source = new ManualObservable<int>();
        var values = new List<int>();
        var command = new RelayCommand<int>(values.Add);
        using CommandInvocationResult result = source.InvokeCommand(command);

        source.Emit(42);

        CollectionAssert.AreEqual(new[] { 42 }, values);
        Assert.IsInstanceOfType<IDisposable>(result);
    }

    [TestMethod]
    public void InvokeCommand_AsyncCommandWithoutModeIsRejected()
    {
        var source = new ManualObservable<int>();
        IAsyncRelayCommand<int> command = new AsyncRelayCommand<int>(_ => Task.CompletedTask);

        var exception = Assert.ThrowsExactly<ArgumentException>(() => source.InvokeCommand(command));

        StringAssert.Contains(exception.Message, nameof(AsyncConcurrencyMode));
    }

    [TestMethod]
    public void InvokeCommand_PropertyOverloadRejectsAsyncCommandThroughErrorStream()
    {
        var source = new ManualObservable<int>();
        var target = new CommandOwner
        {
            Command = new AsyncRelayCommand<int>(_ => Task.CompletedTask),
        };
        using var result = source.InvokeCommand(target, owner => owner.Command);
        var errors = new List<Exception>();
        result.ThrownExceptions.Subscribe(errors.Add);

        source.Emit(1);

        Assert.HasCount(1, errors);
        Assert.IsInstanceOfType<InvalidOperationException>(errors[0]);
        StringAssert.Contains(errors[0].Message, nameof(AsyncConcurrencyMode));
    }

    [TestMethod]
    public void ObserveCanExecute_PublishesInitialAndChangesUntilDisposed()
    {
        var enabled = false;
        var command = new RelayCommand<int>(_ => { }, _ => enabled);
        var values = new List<bool>();
        var subscription = command.ObserveCanExecute(1).Subscribe(values.Add);

        enabled = true;
        command.NotifyCanExecuteChanged();
        subscription.Dispose();
        enabled = false;
        command.NotifyCanExecuteChanged();

        CollectionAssert.AreEqual(new[] { false, true }, values);
    }

    [TestMethod]
    public async Task ObserveIsRunning_PublishesInitialAndChangesUntilDisposed()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new AsyncRelayCommand(async () => await release.Task);
        var values = new List<bool>();
        var subscription = command.ObserveIsRunning().Subscribe(values.Add);

        var execution = command.ExecuteAsync(null);
        await WaitUntilAsync(() => values.Contains(true));
        release.SetResult();
        await execution;
        await WaitUntilAsync(() => values.Count >= 3);
        subscription.Dispose();

        CollectionAssert.AreEqual(new[] { false, true, false }, values);
    }

    [TestMethod]
    public async Task InvokeCommand_SequentialQueuesAndDropIgnoresOverlaps()
    {
        var sequentialSource = new ManualObservable<int>();
        var sequentialStarted = new List<int>();
        var firstRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sequential = new AsyncRelayCommand<int>(async value =>
        {
            sequentialStarted.Add(value);
            if (value == 1)
            {
                await firstRelease.Task;
            }
        });
        using var sequentialResult = sequentialSource.InvokeCommand(sequential, AsyncConcurrencyMode.Sequential);

        sequentialSource.Emit(1);
        sequentialSource.Emit(2);
        CollectionAssert.AreEqual(new[] { 1 }, sequentialStarted);
        firstRelease.SetResult();
        await WaitUntilAsync(() => sequentialStarted.Count == 2);
        CollectionAssert.AreEqual(new[] { 1, 2 }, sequentialStarted);

        var dropSource = new ManualObservable<int>();
        var dropStarted = new List<int>();
        var dropRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var drop = new AsyncRelayCommand<int>(async value =>
        {
            dropStarted.Add(value);
            await dropRelease.Task;
        });
        using var dropResult = dropSource.InvokeCommand(drop, AsyncConcurrencyMode.Drop);
        dropSource.Emit(1);
        dropSource.Emit(2);
        dropRelease.SetResult();
        await drop.ExecutionTask!;

        CollectionAssert.AreEqual(new[] { 1 }, dropStarted);
    }

    [TestMethod]
    public async Task InvokeCommand_LatestCancelsPreviousAndConcurrentStartsAll()
    {
        var latestSource = new ManualObservable<int>();
        var latestStarted = new List<int>();
        var latest = new AsyncRelayCommand<int>(async (value, cancellationToken) =>
        {
            latestStarted.Add(value);
            if (value == 1)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
        });
        using var latestResult = latestSource.InvokeCommand(latest, AsyncConcurrencyMode.Latest);
        latestSource.Emit(1);
        latestSource.Emit(2);
        await WaitUntilAsync(() => latestStarted.Count == 2);
        CollectionAssert.AreEqual(new[] { 1, 2 }, latestStarted);

        var concurrentSource = new ManualObservable<int>();
        var concurrentStarted = new List<int>();
        var concurrentRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var concurrent = new AsyncRelayCommand<int>(
            async value =>
            {
                lock (concurrentStarted)
                {
                    concurrentStarted.Add(value);
                }
                await concurrentRelease.Task;
            },
            AsyncRelayCommandOptions.AllowConcurrentExecutions
        );
        using var concurrentResult = concurrentSource.InvokeCommand(concurrent, AsyncConcurrencyMode.Concurrent);
        concurrentSource.Emit(1);
        concurrentSource.Emit(2);
        await WaitUntilAsync(() =>
        {
            lock (concurrentStarted)
            {
                return concurrentStarted.Count == 2;
            }
        });
        concurrentRelease.SetResult();

        CollectionAssert.AreEquivalent(new[] { 1, 2 }, concurrentStarted);
    }

    [TestMethod]
    public async Task InvokeCommand_AsyncFailurePublishesAndStopsBridge()
    {
        var source = new ManualObservable<int>();
        var expected = new TestException("async execute");
        var command = new AsyncRelayCommand<int>(_ => Task.FromException(expected));
        using var result = source.InvokeCommand(command, AsyncConcurrencyMode.Drop);
        var errors = new List<Exception>();
        result.ThrownExceptions.Subscribe(errors.Add);

        source.Emit(1);
        await WaitUntilAsync(() => errors.Count == 1);
        source.Emit(2);

        CollectionAssert.AreEqual(new[] { expected }, errors);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void InvokeCommand_CommandPathFailureIsReplayedWithoutEscaping()
    {
        var source = new ManualObservable<int>();

        using var result = source.InvokeCommand(new ThrowingCommandOwner(), owner => owner.Command);
        var errors = new List<Exception>();
        result.ThrownExceptions.Subscribe(errors.Add);

        Assert.HasCount(1, errors);
        Assert.IsInstanceOfType<TestException>(errors[0]);
        Assert.AreEqual(0, source.SubscriptionCount);
    }

    [TestMethod]
    public void InvokeCommand_SourceErrorPublishesOnceAndCompletesOnDispose()
    {
        var source = new ManualObservable<int>();
        using var result = source.InvokeCommand(new RelayCommand<int>(_ => { }));
        var expected = new TestException("source");
        var errors = new List<Exception>();
        var completed = false;
        result.ThrownExceptions.Subscribe(errors.Add, error => Assert.Fail(error.ToString()), () => completed = true);

        source.Fail(expected);
        source.Fail(new TestException("second"));

        CollectionAssert.AreEqual(new[] { expected }, errors);
        Assert.IsTrue(completed);
        Assert.AreEqual(1, source.DisposalCount);
    }

    [TestMethod]
    public void InvokeCommand_DisposeStopsSourceAndPropertyTracking()
    {
        var source = new ManualObservable<int>();
        var first = new RecordingCommand();
        var second = new RecordingCommand();
        var target = new CommandOwner { Command = first };
        var subscription = source.InvokeCommand(target, owner => owner.Command);

        subscription.Dispose();
        subscription.Dispose();
        target.Command = second;
        source.Emit(1);

        Assert.AreEqual(0, first.ExecutedValues.Count);
        Assert.AreEqual(0, second.ExecutedValues.Count);
        Assert.AreEqual(1, source.DisposalCount);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class ThrowingCommandOwner : ObservableObject
    {
        public ICommand? Command => throw new TestException("command path");
    }

    private sealed partial class CommandOwner : ObservableObject
    {
        private ICommand? _command;

        public ICommand? Command
        {
            get => _command;
            set => SetProperty(ref _command, value);
        }
    }

    private sealed class RecordingCommand(
        Func<object?, bool>? canExecute = null,
        Action<object?>? execute = null
    ) : ICommand
    {
        public List<object?> CheckedValues { get; } = [];

        public List<object?> ExecutedValues { get; } = [];

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter)
        {
            CheckedValues.Add(parameter);
            return canExecute?.Invoke(parameter) ?? true;
        }

        public void Execute(object? parameter)
        {
            ExecutedValues.Add(parameter);
            execute?.Invoke(parameter);
        }
    }
}
