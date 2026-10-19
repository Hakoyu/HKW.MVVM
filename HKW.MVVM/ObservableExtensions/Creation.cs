namespace HKW.MVVM;

public static partial class ObservableExtensions
{
    /// <summary>
    /// 使用指定的订阅委托创建可观察序列
    /// </summary>
    /// <typeparam name="TSource">序列值类型</typeparam>
    /// <param name="subscribe">为每个观察者调用并返回其订阅资源的委托</param>
    /// <returns>由指定委托实现的可观察序列</returns>
    public static IObservable<TSource> Create<TSource>(
        Func<IObserver<TSource>, IDisposable> subscribe
    )
    {
        ArgumentNullException.ThrowIfNull(subscribe);
        return new AnonymousObservable<TSource>(observer =>
        {
            var subscription = new AutoDetachObserver<TSource>(observer);
            try
            {
                var resource = subscribe(subscription);
                if (resource is null)
                {
                    throw new InvalidOperationException("The subscribe delegate returned null.");
                }

                subscription.SetResource(resource);
            }
            catch (Exception exception)
            {
                if (subscription.ObserverFaulted)
                {
                    throw;
                }

                subscription.OnError(exception);
            }

            return subscription;
        });
    }

    /// <summary>
    /// 创建在每次订阅时调用工厂的可观察序列
    /// </summary>
    /// <typeparam name="TSource">序列值类型</typeparam>
    /// <param name="observableFactory">为每个订阅创建源序列的工厂</param>
    /// <returns>延迟创建源的可观察序列</returns>
    public static IObservable<TSource> Defer<TSource>(
        Func<IObservable<TSource>> observableFactory
    )
    {
        ArgumentNullException.ThrowIfNull(observableFactory);
        return Create<TSource>(observer =>
        {
            var source = observableFactory();
            if (source is null)
            {
                throw new InvalidOperationException("The observable factory returned null.");
            }

            return source.Subscribe(observer);
        });
    }

    private sealed class AutoDetachObserver<T>(IObserver<T> observer) : IObserver<T>, IDisposable
    {
        private readonly SingleAssignmentDisposable _resource = new();
        private int _observerFaulted;
        private int _stopped;

        public bool ObserverFaulted => Volatile.Read(ref _observerFaulted) != 0;

        public void SetResource(IDisposable resource) => _resource.Disposable = resource;

        public void OnNext(T value)
        {
            if (Volatile.Read(ref _stopped) != 0)
            {
                return;
            }

            try
            {
                observer.OnNext(value);
            }
            catch
            {
                Interlocked.Exchange(ref _observerFaulted, 1);
                Dispose();
                throw;
            }
        }

        public void OnError(Exception error)
        {
            ArgumentNullException.ThrowIfNull(error);
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
            {
                return;
            }

            try
            {
                observer.OnError(error);
            }
            catch
            {
                Interlocked.Exchange(ref _observerFaulted, 1);
                throw;
            }
            finally
            {
                _resource.Dispose();
            }
        }

        public void OnCompleted()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
            {
                return;
            }

            try
            {
                observer.OnCompleted();
            }
            catch
            {
                Interlocked.Exchange(ref _observerFaulted, 1);
                throw;
            }
            finally
            {
                _resource.Dispose();
            }
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _stopped, 1);
            _resource.Dispose();
        }
    }
}
