# HKW.MVVM

轻量级 .NET MVVM 响应式辅助库，提供属性观察、派生属性、Observable 操作符、资源释放和日志支持。

本库面向希望使用响应式 MVVM 编程模型、但不希望引入 ReactiveUI 依赖的项目。

## 功能概览

| 功能 | API | 说明 |
| --- | --- | --- |
| 属性观察 | `WhenAnyValue`、`WhenAny` | 观察 1～4 个直接或嵌套属性；订阅时发出当前值，路径节点替换后自动重新绑定 |
| 响应式对象 | `ObservableObjectEx` | 扩展 CommunityToolkit 的 `ObservableObject`，公开通知方法以及 `Changing`、`Changed` 流 |
| 属性绑定 | `BindTo`、`TwoWayBind` | 单向/双向绑定，支持类型转换、自定义赋值和嵌套可写属性路径 |
| 派生属性 | `ObservableAsPropertyHelper<T>`、`ToProperty` | 将流的最新值公开为只读属性，支持初始值、延迟订阅、调度和错误流 |
| Observable 操作符 | `Select`、`Where`、`DistinctUntilChanged`、`StartWith`、`Skip`、`Take`、`Do`、`ObserveOn`、`SubscribeOn`、`Throttle`、`Catch` | 不依赖 System.Reactive 的常用冷 Observable 操作符 |
| Observable 创建与订阅 | `Return`、`Empty`、`Subscribe` | 创建单值/空序列，并通过回调订阅值、错误和完成通知 |
| 资源管理 | `MultipleDisposable`、`DisposeWith` | 线程安全地集中管理订阅和其他 `IDisposable` |
| 日志 | `IEnableLogger`、`IEnableLoggerWithLogger`、`LogHost`、`LogIfEnabled`、Observable `Log` | 使用 `Microsoft.Extensions.Logging` 记录对象日志或完整 Observable 通知 |
| 调度 | `ObservableSchedulers` | 使用捕获的 `SynchronizationContext` 或线程池调度订阅、通知与派生属性更新 |

项目面向 Native AOT，直接属性观察不使用反射；嵌套属性路径会读取路径元数据。运行时不依赖 ReactiveUI 或 System.Reactive。

## 安装

```bash
dotnet add package HKW.MVVM
```

当前包版本为 `0.1.3`，目标框架为 `.NET 10`，并声明支持 Native AOT。

## 快速开始

### 观察属性变化

```csharp
using HKW.MVVM;

public sealed class Person : ObservableObjectEx
{
    public string Name
    {
        get => field;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public int Age
    {
        get => field;
        set => SetProperty(ref field, value);
    }
}

var person = new Person { Name = "Ada" };

using var subscription = person
    .WhenAnyValue(x => x.Name)
    .Subscribe(name => Console.WriteLine(name));

person.Name = "Grace";
```

订阅建立时会先发出当前值，之后只发出发生变化的值。订阅被 Dispose 后，将停止接收属性通知。

观察多个属性时，不提供 selector 会得到强类型元组；也可以提供 selector 直接投影为目标类型：

```csharp
IObservable<(string, int)> nameAndAge = person.WhenAnyValue(x => x.Name, x => x.Age);

IObservable<string> display = person.WhenAnyValue(
    x => x.Name,
    x => x.Age,
    static (name, age) => $"{name} ({age})");
```

最多可以一次观察四个属性。嵌套路径中的任意对象被替换时会自动解绑旧对象并绑定新对象；路径暂时为 `null` 时停止发值，路径恢复后继续观察：

```csharp
using var citySubscription = person
    .WhenAnyValue(x => x.Address.City)
    .Subscribe(Console.WriteLine);
```

需要同时获得发送方、最终属性名和值时，使用 `WhenAny`：

```csharp
using var details = person
    .WhenAny(
        x => x.Name,
        observation => $"{observation.Sender}: {observation.PropertyName} = {observation.Value}")
    .Subscribe(Console.WriteLine);
```

### 属性绑定

使用 `BindTo` 将 Observable 的值单向写入目标属性：

```csharp
using var binding = person
    .WhenAnyValue(x => x.Name)
    .BindTo(view, x => x.Text);

// 源值与目标属性类型不同时使用转换函数
using var ageBinding = person
    .WhenAnyValue(x => x.Age)
    .BindTo(view, x => x.Text, age => age.ToString());
```

也可以直接提供赋值委托。这种形式不解析或编译属性表达式，也不使用反射，适合自定义赋值等场景：

```csharp
using var binding = person
    .WhenAnyValue(x => x.Name)
    .BindTo(view, (value, target) => target.Text = value);
```

使用 `TwoWayBind` 建立双向绑定。建立绑定时，source 的当前值会先初始化 target：

```csharp
using var binding = viewModel.TwoWayBind(
    view,
    source => source.Name,
    target => target.Text);
```

属性类型不同时，可以提供两个方向的转换函数：

```csharp
using var binding = viewModel.TwoWayBind(
    view,
    source => source.Age,
    target => target.Text,
    age => age.ToString(),
    text => int.Parse(text));
```

也可以显式提供两个方向的赋值委托。该重载使用表达式观察属性，但不会解析或编译 Setter：

```csharp
using var binding = viewModel.TwoWayBind(
    view,
    source => source.Age,
    target => target.Text,
    (value, target) => target.Text = value.ToString(),
    (value, source) => source.Age = int.Parse(value));
```

双向绑定两端都需要实现 `INotifyPropertyChanged`。扩展方法调用方是 source，建立绑定时 source 当前值优先初始化 target；内部会抑制回写循环。释放返回的 `IDisposable` 后会停止两个方向的更新。

### 创建派生属性

```csharp
public sealed class PersonViewModel : ObservableObjectEx, IDisposable
{
    private readonly ObservableAsPropertyHelper<string> _displayName;

    public PersonViewModel()
    {
        _displayName = this
            .WhenAnyValue(
                x => x.FirstName,
                x => x.LastName,
                static (firstName, lastName) => $"{firstName} {lastName}".Trim())
            .ToProperty(this, x => x.DisplayName);
    }

    public string FirstName
    {
        get => field;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string LastName
    {
        get => field;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string DisplayName => _displayName.Value;

    public void Dispose() => _displayName.Dispose();
}
```

`ObservableAsPropertyHelper<T>` 会对连续重复值去重，在源值变化时依次通知自身和拥有者的 `PropertyChanging`/`PropertyChanged`。`ToProperty` 支持用表达式或属性名指定目标属性，也支持 `initialValue`、`deferSubscription`、`SynchronizationContext` 或 `ObservableSchedulers`。可通过 `IsSubscribed` 判断是否已经订阅源。

源发生异常时，可以通过 `ThrownExceptions` 观察异常：

```csharp
using var errors = _displayName.ThrownExceptions.Subscribe(
    exception => Console.Error.WriteLine(exception));
```

### 响应式属性通知

`ObservableObjectEx` 继承自 CommunityToolkit 的 `ObservableObject`，额外提供公开的通知方法以及通知流：

```csharp
var model = new Person();

using var changing = model.Changing.Subscribe(change =>
    Console.WriteLine($"Changing: {change.PropertyName}"));
using var changed = model.Changed.Subscribe(change =>
    Console.WriteLine($"Changed: {change.PropertyName}"));

model.NotifyPropertyChanging(nameof(Person.Name));
model.NotifyPropertyChanged(nameof(Person.Name));
```

`ObservableObjectEx` 实现 `IPropertyChangeNotifier`。`Changing` 和 `Changed` 的元素实现 `IPropertyChangedEventArgs<TSender>`，包含 `Sender` 和 `PropertyName`；`WhenAny` 则使用额外包含 `Value` 的 `PropertyObservation<TSender, TValue>`。

### 管理多个订阅

```csharp
using var disposables = new MultipleDisposable();

person.WhenAnyValue(x => x.Name)
    .Subscribe(Console.WriteLine)
    .DisposeWith(disposables);

// 释放全部已注册资源
disposables.Dispose();
```

资源容器为线程安全实现：

- `Clear()`：仅移除当前资源，不释放它们，容器仍可继续使用。
- `DisposeAndClear()`：释放并移除当前资源，容器仍可继续使用。
- `Remove()`：移除单个资源但不释放。
- `Dispose()`：尝试释放全部资源并永久关闭容器；之后添加的资源会被立即释放。多个释放异常会聚合为 `AggregateException`。

## Observable 操作符

所有操作符返回冷 Observable，并保留由 `WhenAnyValue`/`WhenAny` 携带的日志记录器。

| 操作符 | 行为 |
| --- | --- |
| `Select` | 投影每个值 |
| `Where` | 按谓词筛选值 |
| `DistinctUntilChanged` | 使用默认或自定义比较器抑制连续重复值 |
| `StartWith` | 在源订阅前先发出指定值 |
| `Skip` / `Take` | 跳过前 N 个值，或最多获取前 N 个值 |
| `Do` | 在转发值前执行副作用 |
| `ObserveOn` | 将观察者通知按顺序投递到指定上下文或调度器 |
| `SubscribeOn` | 在指定上下文或调度器上执行源订阅 |
| `Throttle` | 静默达到指定时长后发出最新值；支持 `TimeProvider` 和调度器 |
| `Catch` | 源失败后切换到异常处理器返回的替代序列 |

无需引入 System.Reactive 即可创建和订阅简单序列：

```csharp
IObservable<int> one = ObservableExtensions.Return(42);
IObservable<int> none = ObservableExtensions.Empty<int>();

using var subscription = one.Subscribe(
    value => Console.WriteLine(value),
    error => Console.Error.WriteLine(error),
    () => Console.WriteLine("Completed"));
```

## 调度和线程

Observable 默认不会自动切换线程。`ObserveOn` 调度通知，`SubscribeOn` 调度订阅动作：

```csharp
observable.ObserveOn(synchronizationContext);
observable.SubscribeOn(ObservableSchedulers.ThreadPool);
```

- `ObservableSchedulers.Current` 在创建操作符时捕获 `SynchronizationContext.Current`，没有上下文时回退到线程池。
- `ObservableSchedulers.ThreadPool` 始终使用线程池。
- `ObserveOn` 保持值与终止通知的顺序。
- `Throttle` 完成时先发出待处理的最后一个值，再发送完成通知。

UI 应用应根据 UI 框架的线程模型显式选择调度方式。

## 日志

### 为对象启用日志

实现 `IEnableLogger` 后，`this.Log()` 会按运行时类型获取并缓存 `ILogger`。可以直接设置全局工厂，也可以让 `LogHost` 从 `CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default` 获取工厂：

```csharp
using Microsoft.Extensions.Logging;

// 使用应用现有的 ILoggerFactory
LogHost.LoggerFactory = loggerFactory;

public sealed class SearchViewModel : ObservableObjectEx, IEnableLogger
{
    public void Search(string text)
    {
        this.Log().LogInformation("Searching for {Text}", text);

        this.LogIfEnabled(LogLevel.Debug)?.Log(
            "Debug-only search details for {Text}",
            text);
    }
}
```

如对象自带 `ILogger`，实现 `IEnableLoggerWithLogger.Logger`。调用 `LogHost.UseDefaultLoggerFactory()` 可清除显式工厂并恢复从 IoC 获取。

### 记录 Observable 通知

`Log` 会原样转发序列并记录 `OnNext`、`OnError` 和 `OnCompleted`。由实现 `IEnableLogger` 的对象创建的 `WhenAnyValue`/`WhenAny` 流可自动携带日志记录器，内置操作符会继续传递它：

```csharp
using var logged = viewModel
    .WhenAnyValue(x => x.SearchText)
    .Throttle(TimeSpan.FromMilliseconds(300))
    .DistinctUntilChanged()
    .Log("Search text changed")
    .Subscribe(text => Console.WriteLine(text));
```

普通 `IObservable<T>` 不携带日志记录器，应显式传入 `ILogger`。还可以指定 `LogLevel`、为每个值生成消息。默认情况下值和完成使用 `Debug`，错误使用 `Error` 并保留原始异常。

## 错误和资源语义

- Observable 发生错误后会终止当前订阅。
- 属性 getter、`Select`、`Where`、`Do`、`DistinctUntilChanged`、`Catch` 处理器或日志消息工厂抛出的异常会转发为 `OnError`。
- `ObservableAsPropertyHelper<T>` 将源错误发布到 `ThrownExceptions`。
- `Throttle` 在源完成时会先发送待处理的最后一个值，再发送完成通知。
- 所有返回订阅的操作都应由调用方负责释放，推荐使用 `using`、`MultipleDisposable` 或 `DisposeWith`。

## AOT 与性能说明

- 项目启用了 `IsAotCompatible`。
- 直接属性 `WhenAnyValue(x => x.Value)` 使用缓存 getter，不通过反射读取值。
- 嵌套路径需要分析属性元数据，并在中间节点变化时重建相关订阅。
- 表达式绑定的 Setter 会被缓存；提供 `Action` 的 `BindTo`/`TwoWayBind` 重载可完全自定义赋值。
- 日志记录器按运行时类型缓存，Observable 日志记录在订阅前不会执行；调用链仅在需要显示时解析一次并缓存。

## 仓库结构与开发

- `HKW.MVVM`：库项目。
- `HKW.MVVMTest`：MSTest 功能和边界行为测试。
- `HKW.MVVMBenchmark`：属性观察、绑定、操作符和派生属性的 BenchmarkDotNet 基准。

```bash
dotnet test HKW.MVVM.slnx
dotnet run --project HKW.MVVMBenchmark -c Release
```

## 依赖

- [CommunityToolkit.Mvvm](https://www.nuget.org/packages/CommunityToolkit.Mvvm)
- [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions)

## 开源协议

本项目采用 MIT License，详见 [LICENSE.txt](LICENSE.txt)。