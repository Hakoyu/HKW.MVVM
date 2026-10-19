using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
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
    public void InvokeCommand_WhenCanExecuteThrows_TerminatesAndDisposesSource()
    {
        var source = new ManualObservable<int>();
        var expected = new TestException("CanExecute failed.");
        var command = new RecordingCommand(_ => throw expected);
        using var subscription = source.InvokeCommand(command);

        var actual = Assert.ThrowsExactly<TestException>(() => source.Emit(1));
        source.Emit(2);

        Assert.AreSame(expected, actual);
        Assert.AreEqual(1, source.DisposalCount);
        Assert.AreEqual(1, command.CheckedValues.Count);
    }

    [TestMethod]
    public void InvokeCommand_WhenExecuteThrowsSynchronously_DisposesSourceBeforeRethrowing()
    {
        var expected = new TestException("Execute failed.");
        var command = new RecordingCommand(execute: _ => throw expected);
        var source = new SynchronousObservable<int>(observer =>
        {
            observer.OnNext(1);
            observer.OnNext(2);
        });

        var actual = Assert.ThrowsExactly<TestException>(() => source.InvokeCommand(command));

        Assert.AreSame(expected, actual);
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
