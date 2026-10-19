# HKW.MVVM

轻量级 .NET MVVM 响应式辅助库，提供属性观察、派生属性、Observable 操作符、资源释放和日志支持。

本库面向希望使用响应式 MVVM 编程模型、但不希望引入 ReactiveUI 依赖的项目。

## 功能概览

| 功能 | API | 说明 |
| --- | --- | --- |
| 属性观察 | `WhenAnyValue`、`WhenAny`、`ObservableForProperty`、`WhenAnyObservable` | 观察属性值、完整观察信息或 Observable 属性；支持嵌套路径重绑定和切换到最新内部序列 |
| 响应式对象 | `ObservableObjectEx` | 扩展 CommunityToolkit 的 `ObservableObject`，公开通知方法以及 `Changing`、`Changed` 流 |
| 属性绑定 | `BindTo`、`TwoWayBind`、`BindingResult` | 单向/双向绑定，支持类型转换、调度、去重、初始方向、替代值和非抛出式错误流 |
| 派生属性 | `ObservableAsPropertyHelper<T>`、`ToProperty` | 将流的最新值公开为只读属性，支持初始值、延迟订阅、调度和错误流 |
| Observable 创建与异步 | `Create`、`Defer`、`Return`、`Empty`、`FromAsync`、`ToTask`、`FirstAsync`、`Subscribe` | 创建冷序列或任务桥接，并通过回调订阅值、错误和完成通知 |
| 条件与组合 | `TakeWhile`、`SkipWhile`、`CombineLatest`、`Zip`、`WithLatestFrom`、`Switch`、`Merge`、`Concat` | 筛选、配对或组合多个序列 |
| 事件与时间 | `FromEventPattern`、`Timer`、`Interval`、`Delay`、`Timeout`、`Throttle`、`Sample` | 将 .NET 事件和计时器转换为 Observable，并控制通知时间 |
| 多播 | `Publish`、`Replay`、`RefCount` | 在订阅者间共享单个源订阅，并可重放有界历史值 |
| 命令桥接 | `InvokeCommand`、`ObserveCanExecute`、`ObserveIsRunning` | 将序列值传给 `ICommand`/Toolkit 命令，并观察命令状态 |
| 其他 Observable 操作符 | `Select`、`Where`、`SelectMany`、`Scan`、`Retry`、`Finally`、`TakeUntil` 等 | 不依赖 System.Reactive 的转换、生命周期和错误处理操作符 |
| 资源管理 | `MultipleDisposable`、`DisposeWith` | 线程安全地集中管理订阅和其他 `IDisposable` |
| 日志 | `IEnableLogger`、`IEnableLoggerWithLogger`、`LogHost`、`LogIfEnabled`、Observable `Log` | 使用 `Microsoft.Extensions.Logging` 记录对象日志或完整 Observable 通知 |
| 调度 | `IObservableScheduler`、`ObservableSchedulers` | 使用立即、当前线程蹦床、线程池或指定 `SynchronizationContext` 调度订阅、通知与派生属性更新 |

项目面向 Native AOT，直接属性观察不使用反射；嵌套属性路径会读取路径元数据。运行时不依赖 ReactiveUI 或 System.Reactive。

## 安装

```bash
dotnet add package HKW.MVVM
```

当前包版本为 `0.2.0`，同时提供 `net8.0` 和 `net10.0` 资产，并声明支持 Native AOT。

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

订阅建立时会先发出当前值，之后只发出发生变化的值。单属性重载可传入 `WhenAnyValueOptions<T>`，配置 `Comparer`、`SkipInitial`，以及嵌套路径不可用时的 `PathUnavailableFallback`；未配置 fallback 时保持暂停发值的行为。订阅被 Dispose 后，将停止接收属性通知。

观察多个属性时，不提供 selector 会得到强类型元组；也可以提供 selector 直接投影为目标类型：

```csharp
IObservable<(string, int)> nameAndAge = person.WhenAnyValue(x => x.Name, x => x.Age);

IObservable<string> display = person.WhenAnyValue(
    x => x.Name,
    x => x.Age,
    static (name, age) => $"{name} ({age})");
```

最多可以一次观察八个属性。嵌套路径中的任意对象被替换时会自动解绑旧对象并绑定新对象；路径暂时为 `null` 时停止发值，路径恢复后继续观察：

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

双向绑定两端都需要实现 `INotifyPropertyChanged`。扩展方法调用方是 source，建立绑定时 source 当前值优先初始化 target；内部会抑制回写循环。返回值是实现 `IDisposable` 的 `BindingResult`，因此原有 `IDisposable` 赋值保持兼容。

`BindingOptions<T>` 和 `TwoWayBindingOptions<TSource, TTarget>` 可配置 `IEqualityComparer<T>`、`IObservableScheduler`、类型安全的 fallback/null 替代值，以及 `SourceToTarget`、`TargetToSource` 或 `None` 初始同步方向：

```csharp
using BindingResult binding = viewModel.TwoWayBind(
    view,
    source => source.Name,
    target => target.Text,
    new TwoWayBindingOptions<string, string>
    {
        InitialSyncDirection = BindingInitialSyncDirection.TargetToSource,
        Scheduler = ObservableSchedulers.CurrentThread,
        TargetNullValue = "(none)"
    });

using var errors = binding.ThrownExceptions.Subscribe(Console.Error.WriteLine);
```

源、转换器或 setter 的首个致命错误会发送到 `ThrownExceptions` 并停止绑定，不会从通知回调逃逸；后续错误被忽略。错误流会保留该错误供稍后订阅，并在 `BindingResult.Dispose()` 时完成。

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

`ObservableAsPropertyHelper<T>` 会对连续重复值去重，在源值变化时依次通知自身和拥有者的 `PropertyChanging`/`PropertyChanged`。`ToProperty` 支持用表达式或属性名指定目标属性，也支持 `initialValue`、旧有的调度重载，或统一的 `ObservableAsPropertyOptions<T>`（`Scheduler`、`Comparer`、`DeferSubscription`）。可通过 `IsSubscribed` 判断是否已经订阅源。

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
| `Select` / `Where` | 投影或筛选值 |
| `DistinctUntilChanged` / `Distinct` | 抑制连续重复值，或在整个订阅期间只保留首次出现的值；支持自定义比较器 |
| `StartWith` / `Prepend` / `Append` / `Skip` / `Take` | 在序列边缘添加值、跳过前 N 个值或最多获取前 N 个值 |
| `TakeWhile` / `SkipWhile` / `TakeLast` / `SkipLast` | 按条件处理起始值，或在成功完成时处理末尾 N 个值 |
| `Pairwise` | 将每个值与它的前一个值组成 `(Previous, Current)` 元组 |
| `Count` / `Aggregate` / `ToList` | 在成功完成时发出计数、聚合结果或收集的列表 |
| `Any` / `All` / `Contains` / `DefaultIfEmpty` | 执行常用量词判断或为空序列提供默认值 |
| `IgnoreElements` / `OfType` / `Cast` / `Repeat` | 忽略值、按运行时类型筛选/转换，或重复订阅已完成的源 |
| `CombineLatest` | 组合 2～4 个源的最新值；每个源至少发值一次后，返回元组或 selector 结果 |
| `Zip` / `WithLatestFrom` | 按索引配对两个源，或由主源触发并附加另一个源的最新值 |
| `Switch` | 只转发最新内部 Observable 的通知 |
| `Merge` / `Concat` | 并发交错多个序列，或按顺序逐个订阅序列 |
| `SelectMany` | 将每个值仅投影为 `IObservable<TResult>`，并并发合并内部序列 |
| `Scan` | 使用首值或显式 seed 累积状态，并发出每次累积结果 |
| `Retry` | 出错后重新订阅；支持异常谓词、固定延迟、指数退避和可测试的 `TimeProvider`；`retryCount` 是额外重试次数 |
| `Finally` | 完成、错误、订阅失败或释放时恰好执行一次操作 |
| `TakeUntil` | 另一序列首次发值时完成；忽略其完成，但转发其错误 |
| `Do` | 在订阅、值、错误、完成和释放生命周期点执行副作用 |
| `ObserveOn` / `SubscribeOn` | 调度观察者通知或源订阅 |
| `Buffer` | 按数量、时间或两者中先到的边界收集并批量发出值 |
| `Throttle` | 静默达到指定时长后发出最新值；支持 `TimeProvider` 和调度器 |
| `Delay` | 延迟所有通知并保持顺序，包括错误和完成通知 |
| `Timeout` | 静默超时后发送 `TimeoutException`，或切换到指定 fallback Observable |
| `Sample` | 在周期 tick 发出期间最新值；源完成后在下一个 tick 完成 |
| `FromEventPattern` / `Timer` / `Interval` | 将事件、单次计时器或周期计时器转换为 Observable |
| `Publish` / `Replay` / `RefCount` | 显式连接共享源、重放有界历史，或按订阅者数量自动连接 |
| `Catch` | 源失败后切换到异常处理器返回的替代序列 |

### 创建与异步桥接

`Create` 负责每个订阅的资源，`Defer` 在订阅时才调用工厂；`FromAsync` 支持已有任务、任务工厂和可取消工厂。`ToTask`、`FirstAsync`、`FirstOrDefaultAsync`、`LastAsync`、`LastOrDefaultAsync`、`SingleAsync` 和 `SingleOrDefaultAsync` 将序列转换回任务。`FromAsyncEnumerable`/`ToAsyncEnumerable` 在 Observable 与异步序列之间转换，`SelectAsync` 则要求显式指定 `AsyncConcurrencyMode.Latest`、`Sequential`、`Drop` 或 `Concurrent`：

```csharp
var deferred = ObservableExtensions.Defer(() =>
    ObservableExtensions.Create<int>(observer =>
    {
        observer.OnNext(ReadCurrentValue());
        observer.OnCompleted();
        return new MultipleDisposable();
    }));

var result = await ObservableExtensions
    .FromAsync(token => service.LoadAsync(token))
    .FirstAsync(cancellationToken);

var projected = source.SelectAsync(
    (value, token) => service.TransformAsync(value, token),
    AsyncConcurrencyMode.Sequential);

await foreach (var value in projected.ToAsyncEnumerable(cancellationToken: cancellationToken))
{
    Consume(value);
}
```

异步枚举桥接默认使用容量为 `16` 的有界通道，并采用等待式背压；可通过 `capacity` 指定其他正数容量。`ToAsyncEnumerable` 为了让同步 `OnNext` 等待通道空间，会在专用线程池任务中订阅源。不要让生产者、消费者和必须继续执行的单线程调度器相互等待，否则仍可能死锁；UI/单线程上下文中应避免阻塞消费，并显式选择合适的调度器。取消或释放枚举会释放源订阅。

### 条件与组合

```csharp
var paired = temperatures
    .TakeWhile(static value => value < 100)
    .Zip(labels, static (value, label) => $"{label}: {value}");

var enriched = requests.WithLatestFrom(
    settings,
    static (request, latestSettings) => (request, latestSettings));
```

`Zip` 等待两侧按顺序配对；`WithLatestFrom` 仅由主源触发，并在另一源产生首值前忽略主源值。

### 事件、时间与多播

```csharp
var clicks = ObservableExtensions.FromEventPattern<EventArgs>(
    handler => button.Click += handler,
    handler => button.Click -= handler);

var ticks = ObservableExtensions.Interval(TimeSpan.FromSeconds(1));
var shared = ticks.Replay(bufferSize: 1).RefCount();

using var clickSubscription = clicks.Subscribe(pattern => Console.WriteLine(pattern.Sender));
using var tickSubscription = shared.Subscribe(Console.WriteLine);
```

`Publish`/`Replay` 返回 `IConnectableObservable<T>`，可调用 `Connect()` 显式控制单个共享源订阅；`RefCount` 在首个订阅者到达时连接，并在最后一个订阅者离开时断开。

### 属性序列与命令桥接

```csharp
using var propertyChanges = viewModel
    .ObservableForProperty(x => x.SearchText, skipInitial: true)
    .Subscribe(change => Console.WriteLine(change.Value));

using var latestResults = viewModel
    .WhenAnyObservable(x => x.ResultsStream)
    .Subscribe(Render);

using var commandBridge = viewModel
    .WhenAnyValue(x => x.SelectedItem)
    .InvokeCommand(viewModel, x => x.OpenItemCommand);
```

`ObservableForProperty` 可选择更改前/后通知、是否跳过初始值及是否去重；`WhenAnyValue`/`WhenAnyObservable` 支持最多八个属性，并会在嵌套路径变化时重新绑定；`WhenAnyChanging`/`WhenAnyChanged` 可显式选择通知阶段。`InvokeCommand` 返回可赋给 `IDisposable` 的 `CommandInvocationResult`；其 `ThrownExceptions` 会发布一次源、属性路径、`CanExecute` 或 `Execute` 终止异常，然后完成并停止桥接。Toolkit `IRelayCommand<T>` 可直接使用；`IAsyncRelayCommand<T>` 必须显式选择 `Latest`、`Sequential`、`Drop` 或 `Concurrent`。`ObserveCanExecute` 和 `ObserveIsRunning` 会先发布当前值，并在释放订阅时解绑事件。

```csharp
using var commandBridge = selections.InvokeCommand(
    viewModel.LoadCommand,
    AsyncConcurrencyMode.Latest
);
using var commandErrors = commandBridge.ThrownExceptions.Subscribe(LogError);
using var canLoad = viewModel.LoadCommand.ObserveCanExecute(default(Item)).Subscribe(UpdateEnabled);
using var loading = viewModel.LoadCommand.ObserveIsRunning().Subscribe(UpdateBusy);
```

`Buffer` 返回 `IObservable<IList<T>>`。数量模式在缓冲区满时发出；时间模式会在每个窗口结束时发出，包括空窗口；组合模式在时间或数量边界先到时发出并开启新窗口：

```csharp
using var batches = observable
    .Buffer(TimeSpan.FromSeconds(1), 100)
    .Subscribe(batch => Console.WriteLine($"Received {batch.Count} values"));
```

源成功完成时会先发出剩余的非空缓冲区；源发生错误时则丢弃尚未发出的值。

`CombineLatest` 提供 2～4 个源的强类型元组和 selector 重载。例如同时计算表单有效性；`TakeUntil` 可在视图停用时结束订阅：

```csharp
var canSave = viewModel.WhenAnyValue(x => x.Name)
    .CombineLatest(
        viewModel.WhenAnyValue(x => x.Email),
        static (name, email) => !string.IsNullOrWhiteSpace(name) && email.Contains('@'));

using var validation = canSave
    .TakeUntil(viewModel.WhenAnyValue(x => x.IsClosed).Where(static closed => closed))
    .BindTo(view, x => x.SaveButtonEnabled);
```

搜索等“最新请求获胜”场景可使用 `Select(...).Switch()`；`SelectMany` 只接受返回 `IObservable<TResult>` 的 projection，不提供 `IEnumerable<TResult>` 重载：

```csharp
using var results = viewModel.WhenAnyValue(x => x.SearchText)
    .Throttle(TimeSpan.FromMilliseconds(300))
    .DistinctUntilChanged()
    .Select(text => searchService.Search(text).Timeout(TimeSpan.FromSeconds(5)))
    .Switch()
    .Retry(2) // 初次请求失败后最多额外重试两次
    .Finally(() => viewModel.IsSearching = false)
    .Subscribe(items => viewModel.Results = items);
```

`Merge` 适合合并多个并行 UI 事件流，`Concat` 适合严格顺序执行，`Scan` 可生成计数或增量状态。时间操作符均支持 `TimeProvider` 重载；`Delay` 也会延迟错误，`Sample` 会在源完成后的下一个采样 tick 完成，`Timeout` 可发送 `TimeoutException` 或切换到 fallback：

```csharp
var status = onlineStatus
    .Timeout(TimeSpan.FromSeconds(10), ObservableExtensions.Return(false))
    .Delay(TimeSpan.FromMilliseconds(100));

var saveCount = saveCommands.Merge(autoSaveCommands)
    .Scan(0, static (count, _) => count + 1)
    .Sample(TimeSpan.FromSeconds(1));
```

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

Observable 默认不会自动切换线程。0.2.0 使用库自有的轻量 `IObservableScheduler` 替代外部调度器类型；`ObserveOn` 调度通知，`SubscribeOn` 调度订阅动作。旧的 `SynchronizationContext` 重载仍可用，并会适配为 `ObservableSchedulers.FromSynchronizationContext(...)`：

```csharp
observable.ObserveOn(synchronizationContext);
observable.SubscribeOn(ObservableSchedulers.ThreadPool);
```

- `ObservableSchedulers.Immediate` 在调用线程上立即执行。
- `ObservableSchedulers.CurrentThread` 在当前线程使用 FIFO 蹦床队列，嵌套调度不会递归执行。
- `ObservableSchedulers.ThreadPool` 始终使用线程池。
- `ObservableSchedulers.FromSynchronizationContext(context)` 将工作投递到指定上下文。
- 所有调度都返回可释放的取消句柄；`ObserveOn` 保持值与终止通知的顺序。
- `Throttle` 完成时先发出待处理的最后一个值，再发送完成通知。

UI 应用应根据 UI 框架的线程模型显式选择调度方式。

## 扩展方法歧义

HKW.MVVM 的若干操作符名称也存在于 System.Reactive、ReactiveUI、LINQ 或异步 LINQ 中。同时导入这些命名空间时，编译器可能报告扩展方法调用不明确。应避免为同一个调用链混用操作符实现，或使用静态调用/别名明确选择本库：

```csharp
using HkwObservable = HKW.MVVM.ObservableExtensions;

var filtered = HkwObservable.Where(source, static value => value > 0);
var result = HkwObservable.SelectAsync(
    filtered,
    static (value, _) => Task.FromResult(value * 2),
    AsyncConcurrencyMode.Sequential);
```

不要通过调整 `using` 顺序来依赖偶然的重载解析；互操作边界应显式转换或限定静态类。

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

可以为一组对象注册专用日志域。域类型可为类或接口；派生类和接口实现类会自动使用该域的工厂，但日志类别仍是对象的运行时类型：

```csharp
using var viewModelLogDomain =
    LogHost.RegisterDomain<ViewModelBase>(viewModelLoggerFactory);

// MainViewModel : ViewModelBase
// 返回由 viewModelLoggerFactory 创建、类别为 MainViewModel 的 ILogger
var logger = mainViewModel.Log();
```

当多个域匹配时，类域优先于接口域，类域选择继承链上最近的类型；多个接口域选择最近注册且尚未释放的域。同一类型可嵌套注册，释放内层注册后会恢复外层域。注册句柄不会接管或释放传入的 `ILoggerFactory`。

如对象自带 `ILogger`，实现 `IEnableLoggerWithLogger.Logger`；自带日志记录器优先于日志域。调用 `LogHost.UseDefaultLoggerFactory()` 可清除显式全局工厂并恢复从 IoC 获取。

### 记录 Observable 通知

`Log` 会原样转发序列并记录 `OnNext`、`OnError` 和 `OnCompleted`。`Trace` 是复用同一实现并固定为 `LogLevel.Trace` 的便利入口，不会产生重复日志。由实现 `IEnableLogger` 的对象创建的 `WhenAnyValue`/`WhenAny` 流可自动携带日志记录器，内置操作符会继续传递它：

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
- `Buffer` 在源完成时会先发送剩余的非空缓冲区；源错误时丢弃尚未发送的缓冲区。
- `Throttle` 在源完成时会先发送待处理的最后一个值，再发送完成通知。
- `Delay` 延迟值、完成和错误；`Sample` 立即转发错误，但将正常完成延至下一个采样 tick。
- `Timeout` 在静默超过期限后发送 `TimeoutException`；提供 fallback 时改为切换序列。
- `Retry(n)` 最多执行 `1 + n` 次订阅；异常谓词拒绝的错误不会重试，延迟/退避重载通过 `TimeProvider` 安排下一次订阅；`Finally` 对每个订阅恰好执行一次。
- `BindingResult.ThrownExceptions` 保留首个终止错误供稍后订阅，并在结果释放时完成；`CommandInvocationResult.ThrownExceptions` 发布首个终止错误后立即完成。两者都停止各自绑定/命令桥接，错误不会从通知回调逃逸。
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