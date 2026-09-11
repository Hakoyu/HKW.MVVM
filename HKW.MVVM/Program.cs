using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HKW.MVVM;

internal static class Program
{
    private static void Main()
    {
        using var loggerFactory = LoggerFactory.Create(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(new SimpleConsoleLoggerProvider());
            logging.SetMinimumLevel(LogLevel.Information);
        });
        Ioc.Default.ConfigureServices(new LoggerFactoryServiceProvider(loggerFactory));

        var m = new TestModel();
        //m.WhenAnyValue(x => x.Name).ObserveOn().SUn
    }
}

internal class TestModel : ObservableObject, IEnableLogger
{
    public TestModel()
    {
        this.Log().LogInformation("Info");
    }

    public string Name
    {
        get => field;
        set => SetProperty(ref field, value);
    } = string.Empty;
}

internal sealed class LoggerFactoryServiceProvider(ILoggerFactory loggerFactory) : IServiceProvider
{
    public object? GetService(Type serviceType) =>
        serviceType == typeof(ILoggerFactory) ? loggerFactory : null;
}

internal sealed class SimpleConsoleLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new SimpleConsoleLogger(categoryName);

    public void Dispose() { }
}

internal sealed class SimpleConsoleLogger(string categoryName) : ILogger
{
    private readonly string _categoryName = categoryName;

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (!IsEnabled(logLevel))
            return;

        Console.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");

        if (exception is not null)
            Console.WriteLine(exception);
    }
}

internal sealed class NullScope : IDisposable
{
    public static NullScope Instance { get; } = new();

    public void Dispose() { }
}
