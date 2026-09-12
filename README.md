# HKW.MVVM

轻量级 .NET MVVM 响应式辅助库，提供属性观察、派生属性、Observable 操作符、资源释放和日志支持。

本库面向希望使用响应式 MVVM 编程模型、但不希望引入 ReactiveUI 依赖的项目。

## 特性

- 基于 `INotifyPropertyChanged` 的 `WhenAnyValue` 属性观察。
- 支持嵌套属性路径变化后的自动重新绑定，例如 `x => x.Address.City`。
- 提供 `BindTo` 单向绑定和 `TwoWayBind` 双向绑定，并支持双向值转换及自定义赋值。
- 支持 `ObservableAsPropertyHelper<T>`，将 Observable 的最新值暴露为只读属性。
- 提供常用的轻量 Observable 操作符：
  - `Select`
  - `Where`
  - `DistinctUntilChanged`
  - `StartWith`
  - `Skip`
  - `Take`
  - `Do`
  - `ObserveOn`
  - `SubscribeOn`
  - `Throttle`
  - `Catch`
- 提供 `MultipleDisposable` 和 `DisposeWith`，用于集中管理订阅和其他可释放资源。
- 提供基于 `Microsoft.Extensions.Logging` 的日志扩展。
- 使用 CommunityToolkit.Mvvm 提供基础属性通知能力。

## 安装

```bash
dotnet add package HKW.MVVM
```

当前目标框架为 `.NET 10`。

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

### 属性绑定

使用 `BindTo` 将 Observable 的值单向写入目标属性：

```csharp
using var binding = person
    .WhenAnyValue(x => x.Name)
    .BindTo(view, x => x.Text);
```

也可以直接提供赋值委托。这种形式不解析或编译属性表达式，也不使用反射，适合自定义赋值等场景：

```csharp
using var binding = person
    .WhenAnyValue(x => x.Name)
    .BindTo(view, (value, target) => target.Text = value);
```

使用 `TwoWayBind` 建立双向绑定。建立绑定时，source 的当前值会先初始化 target：

```csharp
using var binding = view.TwoWayBind(
    viewModel,
    source => source.Name,
    target => target.Text);
```

属性类型不同时，可以提供两个方向的转换函数：

```csharp
using var binding = view.TwoWayBind(
    viewModel,
    source => source.Age,
    target => target.Text,
    age => age.ToString(),
    text => int.Parse(text));
```

也可以显式提供两个方向的赋值委托。该重载使用表达式观察属性，但不会解析或编译 Setter：

```csharp
using var binding = view.TwoWayBind(
    viewModel,
    source => source.Age,
    target => target.Text,
    (value, target) => target.Text = value.ToString(),
    (value, source) => source.Age = int.Parse(value));
```

双向绑定两端都需要实现 `INotifyPropertyChanged`，释放返回的 `IDisposable` 后会停止两个方向的更新。

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

`ObservableAsPropertyHelper<T>` 会在源值变化时通知自身的 `PropertyChanged`，并通知拥有该只读属性的对象。源发生异常时，可以通过 `ThrownExceptions` 观察异常：

```csharp
using var errors = _displayName.ThrownExceptions.Subscribe(
    exception => Console.Error.WriteLine(exception));
```

### 管理多个订阅

```csharp
using var disposables = new MultipleDisposable();

person.WhenAnyValue(x => x.Name)
    .Subscribe(Console.WriteLine)
    .DisposeWith(disposables);

// 释放全部已注册资源
disposables.Dispose();
```

调用 `Clear()` 会释放当前资源并允许继续添加新资源；调用 `Dispose()` 会永久关闭容器，之后添加的资源会立即被释放。

## 调度和线程

Observable 默认不会自动切换线程。需要将通知投递到指定的 `SynchronizationContext` 时，可以使用：

```csharp
observable.ObserveOn(synchronizationContext);
```

也可以使用 `ObservableSchedulers.Current` 或 `ObservableSchedulers.ThreadPool` 选择当前上下文或线程池。UI 应用应根据 UI 框架的线程模型显式选择调度方式。

## 错误和资源语义

- Observable 发生错误后会终止当前订阅。
- `Select`、`Where`、`Do` 和 `DistinctUntilChanged` 的用户委托发生异常时，会转发为 `OnError` 并释放上游订阅。
- `Throttle` 在源完成时会先发送待处理的最后一个值，再发送完成通知。
- 所有返回订阅的操作都应由调用方负责释放，推荐使用 `using`、`MultipleDisposable` 或 `DisposeWith`。

## 依赖

- [CommunityToolkit.Mvvm](https://www.nuget.org/packages/CommunityToolkit.Mvvm)
- [Microsoft.Extensions.Logging](https://www.nuget.org/packages/Microsoft.Extensions.Logging)

## 开源协议

本项目采用 MIT License，详见 [LICENSE.txt](LICENSE.txt)。