using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.ExceptionServices;
using System.Windows.Input;

namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 将可观察序列产生的每个值传递给命令
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <param name="source">提供命令参数的可观察序列</param>
    /// <param name="command">接收源值的命令</param>
    /// <returns>可停止命令调用的可释放对象</returns>
    /// <remarks>
    /// 每个值都会先传递给 <see cref="ICommand.CanExecute"/>.仅当其返回 <see langword="true"/>
    /// 时才调用 <see cref="ICommand.Execute"/>.命令回调引发异常时,订阅会终止并释放源订阅.
    /// </remarks>
    public static IDisposable InvokeCommand<TSource>(
        this IObservable<TSource> source,
        ICommand command
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(command);

        var subscription = new CommandInvocationSubscription<TSource>();
        subscription.Start(source, command);
        return subscription;
    }

    /// <summary>
    /// 将可观察序列产生的每个值传递给目标对象当前的命令属性
    /// </summary>
    /// <typeparam name="TSource">源值类型</typeparam>
    /// <typeparam name="TTarget">命令属性所属对象的类型</typeparam>
    /// <param name="source">提供命令参数的可观察序列</param>
    /// <param name="target">命令属性所属对象</param>
    /// <param name="commandProperty">以 <paramref name="target"/> 为根的命令属性路径</param>
    /// <returns>可停止属性跟踪和命令调用的可释放对象</returns>
    /// <remarks>
    /// 订阅会跟踪属性路径及其中间对象的替换,并为每个源值使用当时的命令.
    /// 当前命令为 <see langword="null"/> 时会丢弃源值.命令回调引发异常时,
    /// 属性跟踪和源订阅都会终止并释放.
    /// </remarks>
    public static IDisposable InvokeCommand<TSource, TTarget>(
        this IObservable<TSource> source,
        TTarget target,
        Expression<Func<TTarget, ICommand?>> commandProperty
    )
        where TTarget : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(commandProperty);

        var subscription = new CommandInvocationSubscription<TSource>();
        subscription.Start(source, target.WhenAnyValue(commandProperty));
        return subscription;
    }

    private sealed class CommandInvocationSubscription<TSource> : IDisposable
    {
        private readonly Lock _gate = new();
        private readonly SingleAssignmentDisposable _commandSubscription = new();
        private readonly SingleAssignmentDisposable _sourceSubscription = new();
        private ICommand? _command;
        private ExceptionDispatchInfo? _failure;
        private bool _starting = true;
        private bool _stopped;

        public void Start(IObservable<TSource> source, ICommand command)
        {
            _command = command;
            StartSource(source);
        }

        public void Start(IObservable<TSource> source, IObservable<ICommand?> commands)
        {
            try
            {
                _commandSubscription.Disposable = commands.Subscribe(
                    SetCommand,
                    Fail,
                    static () => { }
                );
            }
            catch
            {
                Dispose();
                throw;
            }

            if (HasFailure is false)
            {
                StartSource(source);
            }
            else
            {
                FinishStart();
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _stopped = true;
                _command = null;
            }

            _sourceSubscription.Dispose();
            _commandSubscription.Dispose();
        }

        private bool HasFailure
        {
            get
            {
                lock (_gate)
                {
                    return _failure is not null;
                }
            }
        }

        private void StartSource(IObservable<TSource> source)
        {
            try
            {
                _sourceSubscription.Disposable = source.Subscribe(
                    Invoke,
                    error => Fail(
                        new InvalidOperationException("Observable terminated with an error.", error)
                    ),
                    Dispose
                );
            }
            catch
            {
                Dispose();
                throw;
            }

            FinishStart();
        }

        private void SetCommand(ICommand? command)
        {
            lock (_gate)
            {
                if (_stopped is false)
                {
                    _command = command;
                }
            }
        }

        private void Invoke(TSource value)
        {
            ExceptionDispatchInfo? failure = null;
            lock (_gate)
            {
                var command = _command;
                if (_stopped || command is null)
                {
                    return;
                }

                try
                {
                    if (command.CanExecute(value))
                    {
                        command.Execute(value);
                    }
                }
                catch (Exception exception)
                {
                    failure = Stop(exception);
                }
            }

            Terminate(failure);
        }

        private void Fail(Exception error)
        {
            ExceptionDispatchInfo? failure;
            lock (_gate)
            {
                failure = Stop(error);
            }

            Terminate(failure);
        }

        private ExceptionDispatchInfo? Stop(Exception error)
        {
            if (_stopped)
            {
                return null;
            }

            _stopped = true;
            _command = null;
            _failure = ExceptionDispatchInfo.Capture(error);
            return _starting ? null : _failure;
        }

        private void FinishStart()
        {
            ExceptionDispatchInfo? failure;
            lock (_gate)
            {
                _starting = false;
                failure = _failure;
            }

            if (failure is not null)
            {
                Dispose();
                failure.Throw();
            }
        }

        private void Terminate(ExceptionDispatchInfo? failure)
        {
            if (HasFailure)
            {
                _sourceSubscription.Dispose();
                _commandSubscription.Dispose();
            }

            failure?.Throw();
        }
    }
}
