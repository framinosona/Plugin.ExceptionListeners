# ⚠️ Plugin.ExceptionListeners Documentation

A comprehensive exception listening framework for .NET applications, providing unified exception handling across different exception sources and MAUI platforms.

## 📖 Overview

Plugin.ExceptionListeners is a powerful and flexible library that enables centralized exception handling in .NET applications. It provides a set of listener classes that can capture exceptions from various sources including:

- **First-chance exceptions** - Catch exceptions as they occur, before they're handled
- **Unhandled exceptions** - Catch exceptions that would otherwise terminate your application
- **Unobserved task exceptions** - Catch exceptions from Tasks that haven't been properly awaited
- **Native platform exceptions** (MAUI) - Catch native exceptions from iOS, Android, macOS, and Windows

## 🚀 Quick Start

### Installation

Install the base package for standard .NET applications:

```bash
dotnet add package Plugin.ExceptionListeners
```

For MAUI applications, also install:

```bash
dotnet add package Plugin.ExceptionListeners.Maui
```

### Basic Usage

```csharp
using Plugin.ExceptionListeners.Listeners;

// Set up a global exception handler
void HandleException(object? sender, ExceptionEventArgs e)
{
    Console.WriteLine($"Exception caught: {e.Exception.Message}");
    // Log, report, or handle the exception as needed
}

// Listen for first-chance exceptions
using var firstChanceListener = new CurrentDomainFirstChanceExceptionListener(HandleException);

// Listen for unhandled exceptions
using var unhandledListener = new CurrentDomainUnhandledExceptionListener(HandleException);

// Listen for unobserved task exceptions
using var taskListener = new TaskSchedulerUnobservedTaskExceptionListener(HandleException);

// Your application code here...
```

### MAUI Usage

```csharp
using Plugin.ExceptionListeners.Maui;

// In your MAUI application (e.g., App.xaml.cs)
public partial class App : Application
{
    private NativeUnhandledExceptionListener? _nativeListener;

    public App()
    {
        InitializeComponent();

        // Set up native exception handling
        _nativeListener = new NativeUnhandledExceptionListener(HandleNativeException);

        MainPage = new AppShell();
    }

    private void HandleNativeException(object? sender, ExceptionEventArgs e)
    {
        // Handle native exceptions from iOS/Android/Windows/macOS
        System.Diagnostics.Debug.WriteLine($"Native exception: {e.Exception}");
    }

    protected override void OnSleep()
    {
        _nativeListener?.Dispose();
        base.OnSleep();
    }
}
```

## 📚 Core Components

### Exception Listeners

All exception listeners inherit from the base `ExceptionListener` class and follow a consistent pattern:

- **Automatic subscription** - Listeners automatically subscribe to their respective exception sources upon creation
- **Event-driven** - All exceptions are propagated through a unified `Received` event
- **Disposable** - Proper cleanup and unsubscription when disposed
- **Thread-safe** - Safe to use across multiple threads

### Available Listeners

| Listener | Source | Platform Support |
|----------|---------|-------------------|
| `CurrentDomainFirstChanceExceptionListener` | `AppDomain.CurrentDomain.FirstChanceException` | All .NET platforms |
| `CurrentDomainUnhandledExceptionListener` | `AppDomain.CurrentDomain.UnhandledException` | All .NET platforms |
| `TaskSchedulerUnobservedTaskExceptionListener` | `TaskScheduler.UnobservedTaskException` | All .NET platforms |
| `NativeUnhandledExceptionListener` | Platform-specific native exceptions | MAUI only |

## 🎯 Use Cases

### Comprehensive Exception Monitoring

```csharp
public class ExceptionMonitoringService : IDisposable
{
    private readonly List<ExceptionListener> _listeners = new();

    public ExceptionMonitoringService()
    {
        // Set up comprehensive exception monitoring
        _listeners.Add(new CurrentDomainFirstChanceExceptionListener(LogException));
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(LogCriticalException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(LogTaskException));
    }

    private void LogException(object? sender, ExceptionEventArgs e)
    {
        // Log all exceptions for diagnostics
        Logger.Information("First-chance exception: {Exception}", e.Exception);
    }

    private void LogCriticalException(object? sender, ExceptionEventArgs e)
    {
        // Log critical unhandled exceptions
        Logger.Fatal("Unhandled exception: {Exception}", e.Exception);
    }

    private void LogTaskException(object? sender, ExceptionEventArgs e)
    {
        // Log unobserved task exceptions
        Logger.Error("Unobserved task exception: {Exception}", e.Exception);
    }

    public void Dispose()
    {
        foreach (var listener in _listeners)
        {
            listener.Dispose();
        }
        _listeners.Clear();
    }
}
```

### Exception Reporting and Analytics

```csharp
public class ExceptionReportingService
{
    public void SetupReporting()
    {
        var reporter = new CurrentDomainUnhandledExceptionListener(ReportException);

        // Keep reference to prevent disposal
        // Store in a field or dependency injection container
    }

    private async void ReportException(object? sender, ExceptionEventArgs e)
    {
        try
        {
            // Report to your preferred service (AppCenter, Sentry, etc.)
            await ReportToAnalyticsService(e.Exception);
        }
        catch
        {
            // Prevent exceptions in exception handlers from crashing the app
        }
    }

    private async Task ReportToAnalyticsService(Exception exception)
    {
        // Implementation depends on your analytics service
        // This is just a placeholder
        await Task.CompletedTask;
    }
}
```

## ⚡ Advanced Features

### Custom Exception Listeners

You can create custom exception listeners by inheriting from `ExceptionListener`:

```csharp
public class CustomExceptionListener : ExceptionListener
{
    public CustomExceptionListener(EventHandler<ExceptionEventArgs> received) : base(received)
    {
        // Subscribe to your custom exception source
        MyCustomExceptionSource.ExceptionOccurred += OnCustomException;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from your custom exception source
            MyCustomExceptionSource.ExceptionOccurred -= OnCustomException;
        }
        base.Dispose(disposing);
    }

    private void OnCustomException(object? sender, CustomExceptionEventArgs e)
    {
        OnReceived(sender, e.Exception);
    }
}
```

### Exception Filtering and Transformation

```csharp
public class FilteringExceptionHandler
{
    private readonly HashSet<Type> _ignoredExceptionTypes = new()
    {
        typeof(OperationCanceledException),
        typeof(TaskCanceledException)
    };

    public void SetupFiltering()
    {
        var listener = new CurrentDomainFirstChanceExceptionListener(FilterAndHandle);
    }

    private void FilterAndHandle(object? sender, ExceptionEventArgs e)
    {
        // Skip known harmless exceptions
        if (_ignoredExceptionTypes.Contains(e.Exception.GetType()))
            return;

        // Handle only specific exceptions
        switch (e.Exception)
        {
            case HttpRequestException httpEx:
                HandleNetworkException(httpEx);
                break;
            case UnauthorizedAccessException authEx:
                HandleAuthException(authEx);
                break;
            default:
                HandleGenericException(e.Exception);
                break;
        }
    }

    private void HandleNetworkException(HttpRequestException ex) { /* ... */ }
    private void HandleAuthException(UnauthorizedAccessException ex) { /* ... */ }
    private void HandleGenericException(Exception ex) { /* ... */ }
}
```

## 🏗️ Architecture

### Design Principles

1. **Single Responsibility** - Each listener handles one specific exception source
2. **Open/Closed** - Extensible through inheritance, closed for modification
3. **Dependency Inversion** - Depends on abstractions, not concretions
4. **Resource Management** - Proper cleanup through IDisposable pattern

### Threading Considerations

- All listeners are thread-safe for subscription and disposal
- Exception events may be raised on different threads depending on the source
- Ensure your exception handlers are thread-safe or use appropriate synchronization

### Performance Impact

- **First-chance exceptions**: High frequency, minimal processing recommended
- **Unhandled exceptions**: Low frequency, comprehensive logging acceptable
- **Task exceptions**: Medium frequency, depends on application async patterns
- **Native exceptions**: Platform-dependent, typically low frequency

## 🔧 Development & Testing

- **Framework**: .NET 9.0
- **Testing**: xUnit + FluentAssertions with comprehensive coverage
- **Build**: `dotnet build -c Release`
- **Test**: `dotnet test -c Release`
- **Documentation**: DocFX for API documentation

## 📄 License

This library is licensed under the **MIT License** - see the [LICENSE.md](https://github.com/framinosona/Plugin.ExceptionListeners/blob/main/LICENSE.md) file for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit issues, feature requests, or pull requests on [GitHub](https://github.com/framinosona/Plugin.ExceptionListeners).

### Development Setup

1. Clone the repository
2. Ensure .NET 9.0 SDK is installed
3. Run `dotnet restore` to restore packages
4. Run `dotnet build` to build the solution
5. Run `dotnet test` to execute tests

## 🔗 Related Resources

- [GitHub Repository](https://github.com/framinosona/Plugin.ExceptionListeners)
- [API Reference](api/index.md)
- [Samples and Examples](examples/index.md)
- [Migration Guide](migration/index.md)

---

*Built with ❤️ for modern .NET applications requiring comprehensive exception handling.*
