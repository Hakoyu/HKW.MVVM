using System.ComponentModel;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using HKW.MVVM;

namespace HKW.MVVMBenchmark;

/// <summary>
/// 比较嵌套 WhenAnyValue 路径与等效的直接事件订阅.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class NestedWhenAnyValueBenchmarks
{
    private readonly Root _directCreateRoot = new();
    private readonly Root _whenAnyCreateRoot = new();
    private readonly Root _reactiveUICreateRoot = new();
    private readonly Root _directUpdateRoot = new();
    private readonly Root _whenAnyUpdateRoot = new();
    private readonly Root _reactiveUIUpdateRoot = new();
    private readonly Child _directAlternativeChild = new();
    private readonly Child _whenAnyAlternativeChild = new();
    private readonly Child _reactiveUIAlternativeChild = new();
    private IDisposable? _directSubscription;
    private IDisposable? _whenAnySubscription;
    private IDisposable? _reactiveUISubscription;
    private Child? _directOriginalChild;
    private Child? _whenAnyOriginalChild;
    private Child? _reactiveUIOriginalChild;
    private bool _useDirectAlternative;
    private bool _useWhenAnyAlternative;
    private bool _useReactiveUIAlternative;
    private int _directResult;
    private int _whenAnyResult;
    private int _reactiveUIResult;
    private int _value;

    /// <summary>
/// 创建叶级更新与重新绑定基准测试所使用的订阅.
/// </summary>
    [GlobalSetup]
    public void SetupUpdateSubscriptions()
    {
        ReactiveUI.Builder.BuilderMixins.BuildApp(
            ReactiveUI.Builder.RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices()
        );
        _directOriginalChild = _directUpdateRoot.Child;
        _whenAnyOriginalChild = _whenAnyUpdateRoot.Child;
        _reactiveUIOriginalChild = _reactiveUIUpdateRoot.Child;
        _directSubscription = new DirectNestedSubscription(
            _directUpdateRoot,
            value => _directResult = value
        );
        _whenAnySubscription = _whenAnyUpdateRoot
            .WhenAnyValue(root => root.Child.Value)
            .Subscribe(value => _whenAnyResult = value);
        _reactiveUISubscription = ReactiveUI
            .WhenAnyMixins.WhenAnyValue(_reactiveUIUpdateRoot, root => root.Child.Value)
            .Subscribe(value => _reactiveUIResult = value);
    }

    /// <summary>
/// 释放更新基准测试所使用的订阅.
/// </summary>
    [GlobalCleanup]
    public void CleanupUpdateSubscriptions()
    {
        _directSubscription?.Dispose();
        _whenAnySubscription?.Dispose();
        _reactiveUISubscription?.Dispose();
    }

    #region Core
    /// <summary>
/// 测量直接嵌套订阅的创建、初始发布和释放.
/// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CreateAndDispose")]
    public void DirectCreateAndDispose()
    {
        using var subscription = new DirectNestedSubscription(
            _directCreateRoot,
            value => _directResult = value
        );
    }

    /// <summary>
/// 测量通过直接嵌套订阅进行的叶级更新.
/// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("LeafUpdate")]
    public void DirectLeafUpdate() => _directUpdateRoot.Child.Value = ++_value;

    /// <summary>
/// 测量替换中间对象后的直接解绑与重新绑定.
/// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("IntermediateRebind")]
    public void DirectIntermediateRebind()
    {
        _useDirectAlternative = !_useDirectAlternative;
        var child = _useDirectAlternative ? _directAlternativeChild : _directOriginalChild!;
        child.Value = ++_value;
        _directUpdateRoot.Child = child;
    }
    #endregion

    #region HKW
    /// <summary>
/// 测量嵌套路径解析、反射订阅、初始发布和释放.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("CreateAndDispose")]
    public void WhenAnyValueCreateAndDispose()
    {
        using var subscription = _whenAnyCreateRoot
            .WhenAnyValue(root => root.Child.Value)
            .Subscribe(value => _whenAnyResult = value);
    }

    /// <summary>
/// 测量通过嵌套 WhenAnyValue 进行的叶级更新.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("LeafUpdate")]
    public void WhenAnyValueLeafUpdate() => _whenAnyUpdateRoot.Child.Value = ++_value;

    /// <summary>
/// 测量替换中间对象后 WhenAnyValue 的解绑与重新绑定.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("IntermediateRebind")]
    public void WhenAnyValueIntermediateRebind()
    {
        _useWhenAnyAlternative = !_useWhenAnyAlternative;
        var child = _useWhenAnyAlternative ? _whenAnyAlternativeChild : _whenAnyOriginalChild!;
        child.Value = ++_value;
        _whenAnyUpdateRoot.Child = child;
    }
    #endregion

    #region ReactiveUI
    /// <summary>
/// 测量 ReactiveUI 嵌套 WhenAnyValue 的创建和释放.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("CreateAndDispose")]
    public void ReactiveUIWhenAnyValueCreateAndDispose()
    {
        using var subscription = ReactiveUI
            .WhenAnyMixins.WhenAnyValue(_reactiveUICreateRoot, root => root.Child.Value)
            .Subscribe(value => _reactiveUIResult = value);
    }

    /// <summary>
/// 测量通过 ReactiveUI 嵌套 WhenAnyValue 进行的叶级更新.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("LeafUpdate")]
    public void ReactiveUIWhenAnyValueLeafUpdate() => _reactiveUIUpdateRoot.Child.Value = ++_value;

    /// <summary>
/// 测量替换中间对象后的 ReactiveUI 重新订阅.
/// </summary>
    [Benchmark]
    [BenchmarkCategory("IntermediateRebind")]
    public void ReactiveUIWhenAnyValueIntermediateRebind()
    {
        _useReactiveUIAlternative = !_useReactiveUIAlternative;
        var child = _useReactiveUIAlternative
            ? _reactiveUIAlternativeChild
            : _reactiveUIOriginalChild!;
        child.Value = ++_value;
        _reactiveUIUpdateRoot.Child = child;
    }
    #endregion


    private sealed class Root : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs ChildChangedEventArgs = new(nameof(Child));
        private Child _child = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public Child Child
        {
            get => _child;
            set
            {
                if (ReferenceEquals(_child, value))
                {
                    return;
                }

                _child = value;
                PropertyChanged?.Invoke(this, ChildChangedEventArgs);
            }
        }
    }

    private sealed class Child : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs ValueChangedEventArgs = new(nameof(Value));
        private int _value;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Value
        {
            get => _value;
            set
            {
                if (_value == value)
                {
                    return;
                }

                _value = value;
                PropertyChanged?.Invoke(this, ValueChangedEventArgs);
            }
        }
    }

    private sealed class DirectNestedSubscription : IDisposable
    {
        private readonly Root _root;
        private readonly Action<int> _onNext;
        private Child? _child;
        private int _lastValue;
        private bool _hasValue;
        private bool _disposed;

        public DirectNestedSubscription(Root root, Action<int> onNext)
        {
            _root = root;
            _onNext = onNext;
            _root.PropertyChanged += OnRootPropertyChanged;
            AttachChild();
            PublishCurrentValue();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _root.PropertyChanged -= OnRootPropertyChanged;
            if (_child is not null)
            {
                _child.PropertyChanged -= OnChildPropertyChanged;
                _child = null;
            }
        }

        private void OnRootPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        {
            if (
                string.IsNullOrEmpty(eventArgs.PropertyName)
                || eventArgs.PropertyName == nameof(Root.Child)
            )
            {
                AttachChild();
                PublishCurrentValue();
            }
        }

        private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        {
            if (
                string.IsNullOrEmpty(eventArgs.PropertyName)
                || eventArgs.PropertyName == nameof(Child.Value)
            )
            {
                PublishCurrentValue();
            }
        }

        private void AttachChild()
        {
            if (_child is not null)
            {
                _child.PropertyChanged -= OnChildPropertyChanged;
            }

            _child = _root.Child;
            _child.PropertyChanged += OnChildPropertyChanged;
        }

        private void PublishCurrentValue()
        {
            var value = _root.Child.Value;
            if (_hasValue && _lastValue == value)
            {
                return;
            }

            _hasValue = true;
            _lastValue = value;
            _onNext(value);
        }
    }
}
