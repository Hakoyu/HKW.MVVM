# HKW.MVVM

轻量级 .NET MVVM 响应式辅助库，提供属性观察、派生属性、Observable 操作符、资源释放和日志支持。

本库面向希望使用响应式 MVVM 编程模型、但不希望引入 ReactiveUI 或 `System.Reactive` 依赖的项目。

## 特性

- 基于 `INotifyPropertyChanged` 的 `WhenAnyValue` 属性观察。
- 支持嵌套属性路径变化后的自动重新绑定，例如 `x => x.Address.City`。
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
}

var person = new Person { Name = "Ada" };

using var subscription = person
    .WhenAnyValue(x => x.Name)
    .Subscribe(name => Console.WriteLine(name));

person.Name = "Grace";
```

订阅建立时会先发出当前值，之后只发出发生变化的值。订阅被 Dispose 后，将停止接收属性通知。

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
- 本库提供的是轻量 Observable 抽象，不承诺与 System.Reactive 的全部 API 或全部调度器实现兼容。

## 依赖

- [CommunityToolkit.Mvvm](https://www.nuget.org/packages/CommunityToolkit.Mvvm)
- [Microsoft.Extensions.Logging](https://www.nuget.org/packages/Microsoft.Extensions.Logging)

本库不会传递引入 ReactiveUI 或 `System.Reactive`。

## 开源协议

本项目采用 MIT License，详见 [LICENSE.txt](LICENSE.txt)。