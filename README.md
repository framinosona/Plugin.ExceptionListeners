# ⚠️ Plugin.ExceptionListeners

<div align="center">

[![Icon](icon.png)](https://github.com/laerdal/Plugin.ExceptionListeners)

</div>

[![CI](https://img.shields.io/github/actions/workflow/status/laerdal/Plugin.ExceptionListeners/ci.yml?logo=github)](https://github.com/laerdal/Plugin.ExceptionListeners/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/nuget/v/Plugin.ExceptionListeners?logo=nuget&color=004880)](https://www.nuget.org/packages/Plugin.ExceptionListeners)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Plugin.ExceptionListeners?logo=nuget&color=004880)](https://www.nuget.org/packages/Plugin.ExceptionListeners)
[![GitHub Release](https://img.shields.io/github/v/release/laerdal/Plugin.ExceptionListeners?logo=github)](https://github.com/laerdal/Plugin.ExceptionListeners/releases)
[![License](https://img.shields.io/github/license/laerdal/Plugin.ExceptionListeners?color=blue)](LICENSE.md)
[![GitHub Pages](https://img.shields.io/badge/docs-GitHub%20Pages-blue?logo=github)](https://laerdal.github.io/Plugin.ExceptionListeners/)

A comprehensive exception listening framework for .NET applications, providing unified exception handling across different exception sources and MAUI cross-platform applications.

---

## 📦 Features

- **First-Chance Exception Listening**: Capture exceptions as they occur, before they're handled by application code
- **Unhandled Exception Monitoring**: Catch exceptions that would otherwise terminate your application
- **Task Exception Handling**: Monitor unobserved exceptions from async Tasks and automatically mark them as observed
- **MAUI Cross-Platform Support**: Native exception handling for iOS, Android, macOS, and Windows platforms
- **Unified Event Model**: All exception sources use the same `ExceptionEventArgs` interface for consistent handling
- **Automatic Resource Management**: Implements `IDisposable` pattern for proper cleanup and event unsubscription
- **Type-Safe Listener Classes**: Strongly typed listener implementations prevent registration errors
- **Background Exception Monitoring**: Non-blocking exception listeners that don't interfere with application performance
- **Platform-Specific Native Handling**: Automatically handles platform-specific exceptions like `NSException`, `Java.Lang.Throwable`, and Win32 exceptions
- **Configurable Listening**: Enable/disable specific exception types based on your monitoring needs
- **Lightweight & Efficient**: Minimal overhead with optimized event handling and memory usage
- **Framework Integration**: Easy integration with logging frameworks like Serilog, NLog, and Microsoft.Extensions.Logging
- **Developer-Friendly API**: Simple, intuitive interface that follows .NET conventions and best practices

---

## 🛠️ Usage Examples

### Basic Exception Handling

```csharp
using Plugin.ExceptionListeners.Listeners;

// Define a global exception handler
void HandleException(object? sender, ExceptionEventArgs e)
{
    Console.WriteLine($"Exception from {sender?.GetType().Name}: {e.Exception.Message}");
    // Log to your preferred logging framework
    // Send to crash reporting service
    // Notify monitoring systems
}

// Set up exception listeners
using var unhandledListener = new CurrentDomainUnhandledExceptionListener(HandleException);
using var taskListener = new TaskSchedulerUnobservedTaskExceptionListener(HandleException);

// Your application code here - exceptions will be automatically captured
```

### Comprehensive Exception Monitoring

```csharp
using Plugin.ExceptionListeners.Listeners;

public class ExceptionManager : IDisposable
{
    private readonly List<ExceptionListener> _listeners = new();

    public ExceptionManager()
    {
        // Monitor different exception sources
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleCriticalException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException));

        // Only enable first-chance in debug builds due to performance impact
#if DEBUG
        _listeners.Add(new CurrentDomainFirstChanceExceptionListener(HandleFirstChanceException));
#endif
    }

    private void HandleCriticalException(object? sender, ExceptionEventArgs e)
    {
        // Log critical exceptions that could crash the app
        Logger.Fatal(e.Exception, "Unhandled exception occurred");
    }

    private void HandleTaskException(object? sender, ExceptionEventArgs e)
    {
        // Handle fire-and-forget Task exceptions
        Logger.Error(e.Exception, "Unobserved task exception");
    }

    private void HandleFirstChanceException(object? sender, ExceptionEventArgs e)
    {
        // Log all exceptions for debugging (high volume!)
        Logger.Debug(e.Exception, "First chance exception");
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

### MAUI Cross-Platform Exception Handling

```csharp
using Plugin.ExceptionListeners.Listeners;
using Plugin.ExceptionListeners.Maui;

public partial class App : Application
{
    private readonly List<ExceptionListener> _listeners = new();

    public App()
    {
        InitializeComponent();
        SetupExceptionHandling();
    }

    private void SetupExceptionHandling()
    {
        // Standard .NET exception listeners
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleException));

        // MAUI-specific native exception listener
        _listeners.Add(new NativeUnhandledExceptionListener(HandleNativeException));
    }

    private void HandleException(object? sender, ExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Managed Exception: {e.Exception}");
        // Handle .NET managed exceptions
    }

    private void HandleNativeException(object? sender, ExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Native Exception: {e.Exception}");
        // Handle platform-specific native exceptions (NSException, Java exceptions, etc.)
    }

    protected override void CleanUp()
    {
        foreach (var listener in _listeners)
        {
            listener.Dispose();
        }
        _listeners.Clear();
        base.CleanUp();
    }
}
```

### Integration with Logging Frameworks

```csharp
using Plugin.ExceptionListeners.Listeners;
using Microsoft.Extensions.Logging;
using Serilog;

// Integration with Microsoft.Extensions.Logging
public class LoggingExceptionHandler
{
    private readonly ILogger<LoggingExceptionHandler> _logger;
    private readonly List<ExceptionListener> _listeners = new();

    public LoggingExceptionHandler(ILogger<LoggingExceptionHandler> logger)
    {
        _logger = logger;

        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleUnhandledException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException));
    }

    private void HandleUnhandledException(object? sender, ExceptionEventArgs e)
    {
        _logger.LogCritical(e.Exception, "Unhandled exception from {Source}", sender?.GetType().Name);
    }

    private void HandleTaskException(object? sender, ExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unobserved task exception");
    }
}

// Integration with Serilog
public class SerilogExceptionHandler
{
    private readonly ILogger _logger = Log.ForContext<SerilogExceptionHandler>();

    private void HandleException(object? sender, ExceptionEventArgs e)
    {
        _logger.Error(e.Exception, "Exception caught from {Source}: {ExceptionType}",
            sender?.GetType().Name ?? "Unknown",
            e.Exception.GetType().Name);
    }
}
```

### Performance Considerations

```csharp
using Plugin.ExceptionListeners.Listeners;

public class OptimizedExceptionHandling
{
    public void SetupProductionListeners()
    {
        // For production: Only monitor critical exceptions
        var unhandledListener = new CurrentDomainUnhandledExceptionListener(HandleCriticalException);
        var taskListener = new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException);

        // Avoid FirstChanceExceptionListener in production - high performance impact
    }

    public void SetupDevelopmentListeners()
    {
        // For development: Monitor all exceptions for debugging
        var unhandledListener = new CurrentDomainUnhandledExceptionListener(HandleException);
        var taskListener = new TaskSchedulerUnobservedTaskExceptionListener(HandleException);
        var firstChanceListener = new CurrentDomainFirstChanceExceptionListener(HandleFirstChanceException);
    }

    private void HandleCriticalException(object? sender, ExceptionEventArgs e)
    {
        // Minimal, fast logging for production
        Console.WriteLine($"CRITICAL: {e.Exception.GetType().Name}: {e.Exception.Message}");
    }

    private void HandleFirstChanceException(object? sender, ExceptionEventArgs e)
    {
        // Detailed logging for development (can be very verbose!)
        if (ShouldLogException(e.Exception))
        {
            Console.WriteLine($"FirstChance: {e.Exception}");
        }
    }

    private bool ShouldLogException(Exception exception)
    {
        // Filter out noisy exceptions like HttpRequestException, SocketException, etc.
        return !exception.GetType().Namespace?.StartsWith("System.Net") == true;
    }
}
```

### Platform-Specific Exception Handling

```csharp
using Plugin.ExceptionListeners.Listeners;
using Plugin.ExceptionListeners.Maui;

public class PlatformSpecificExceptionHandler
{
    public void SetupPlatformListeners()
    {
#if ANDROID
        // Android-specific exception handling
        var nativeListener = new NativeUnhandledExceptionListener(HandleAndroidException);
        // Automatically handles Java exceptions and JNI exceptions
#elif IOS
        // iOS-specific exception handling
        var nativeListener = new NativeUnhandledExceptionListener(HandleiOSException);
        // Automatically handles NSException and Objective-C exceptions
#elif WINDOWS
        // Windows-specific exception handling
        var nativeListener = new NativeUnhandledExceptionListener(HandleWindowsException);
        // Handles Win32 exceptions and SEH exceptions
#elif MACCATALYST
        // macOS Catalyst exception handling
        var nativeListener = new NativeUnhandledExceptionListener(HandleMacException);
#endif
    }

    private void HandleAndroidException(object? sender, ExceptionEventArgs e)
    {
        // Handle Android native exceptions
        if (e.Exception.Data.Contains("AndroidException"))
        {
            System.Diagnostics.Debug.WriteLine($"Android Native: {e.Exception}");
        }
    }

    private void HandleiOSException(object? sender, ExceptionEventArgs e)
    {
        // Handle iOS native exceptions
        if (e.Exception.Data.Contains("NSException"))
        {
            System.Diagnostics.Debug.WriteLine($"iOS Native: {e.Exception}");
        }
    }

    private void HandleWindowsException(object? sender, ExceptionEventArgs e)
    {
        // Handle Windows native exceptions
        System.Diagnostics.Debug.WriteLine($"Windows Native: {e.Exception}");
    }

    private void HandleMacException(object? sender, ExceptionEventArgs e)
    {
        // Handle macOS Catalyst exceptions
        System.Diagnostics.Debug.WriteLine($"macOS Native: {e.Exception}");
    }
}
```

### Advanced Exception Filtering and Analysis

```csharp
using Plugin.ExceptionListeners.Listeners;

public class AdvancedExceptionAnalyzer
{
    private readonly Dictionary<Type, int> _exceptionCounts = new();
    private readonly List<ExceptionListener> _listeners = new();

    public AdvancedExceptionAnalyzer()
    {
        _listeners.Add(new CurrentDomainFirstChanceExceptionListener(AnalyzeException));
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleCriticalException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException));
    }

    private void AnalyzeException(object? sender, ExceptionEventArgs e)
    {
        var exceptionType = e.Exception.GetType();

        // Count exception frequency
        _exceptionCounts[exceptionType] = _exceptionCounts.GetValueOrDefault(exceptionType) + 1;

        // Filter out common, expected exceptions
        if (IsExpectedException(e.Exception))
        {
            return; // Don't log expected exceptions
        }

        // Log unusual or critical exceptions
        if (IsCriticalException(e.Exception))
        {
            LogCriticalException(e.Exception);
        }

        // Detect exception patterns
        if (_exceptionCounts[exceptionType] > 10)
        {
            LogFrequentException(exceptionType, _exceptionCounts[exceptionType]);
        }
    }

    private bool IsExpectedException(Exception exception)
    {
        // Filter out common framework exceptions
        return exception is HttpRequestException ||
               exception is SocketException ||
               exception is TaskCanceledException ||
               (exception is ArgumentException && exception.Message.Contains("culture"));
    }

    private bool IsCriticalException(Exception exception)
    {
        return exception is OutOfMemoryException ||
               exception is StackOverflowException ||
               exception is AccessViolationException ||
               exception is InvalidProgramException;
    }

    private void LogCriticalException(Exception exception)
    {
        Console.WriteLine($"CRITICAL EXCEPTION: {exception.GetType().Name}: {exception.Message}");
        // Send to monitoring service, crash reporting, etc.
    }

    private void LogFrequentException(Type exceptionType, int count)
    {
        Console.WriteLine($"FREQUENT EXCEPTION: {exceptionType.Name} occurred {count} times");
        // Alert development team about potential issues
    }

    public void PrintStatistics()
    {
        Console.WriteLine("Exception Statistics:");
        foreach (var kvp in _exceptionCounts.OrderByDescending(x => x.Value))
        {
            Console.WriteLine($"  {kvp.Key.Name}: {kvp.Value} occurrences");
        }
    }
}
```

---

## 📋 API Overview

### Core Classes

- **`ExceptionListener`** - Abstract base class for all exception listeners with IDisposable support
- **`ExceptionEventArgs`** - Event arguments containing the Exception instance for unified handling
- **`CurrentDomainFirstChanceExceptionListener`** - Captures first-chance exceptions from AppDomain
- **`CurrentDomainUnhandledExceptionListener`** - Monitors unhandled exceptions that could crash the app
- **`TaskSchedulerUnobservedTaskExceptionListener`** - Handles unobserved Task exceptions automatically
- **`NativeUnhandledExceptionListener`** (MAUI) - Cross-platform native exception monitoring

### Exception Sources

- **First-Chance Exceptions**: `AppDomain.CurrentDomain.FirstChanceException` - All exceptions as they occur
- **Unhandled Exceptions**: `AppDomain.CurrentDomain.UnhandledException` - Exceptions that would terminate the app
- **Task Exceptions**: `TaskScheduler.UnobservedTaskException` - Fire-and-forget Task exceptions
- **Native Exceptions** (MAUI): Platform-specific native exceptions from iOS, Android, Windows, macOS

### Platform Support

- **.NET Core Applications**: Full support for all managed exception listeners
- **MAUI iOS**: NSException, Objective-C exceptions, managed exceptions
- **MAUI Android**: Java exceptions, JNI exceptions, managed exceptions
- **MAUI Windows**: Win32 exceptions, SEH exceptions, managed exceptions
- **MAUI macOS/Catalyst**: Native macOS exceptions, managed exceptions

### Event Handling

- **Unified Interface**: All listeners use `EventHandler<ExceptionEventArgs>` delegates
- **Exception Information**: Access to full Exception object with stack trace and details
- **Source Identification**: Sender parameter identifies which listener caught the exception
- **Automatic Cleanup**: IDisposable pattern ensures proper event unsubscription

### Configuration Options

- **Selective Listening**: Enable only the exception types you need to monitor
- **Performance Control**: First-chance listeners can be disabled in production for performance
- **Platform Targeting**: Conditional compilation for platform-specific exception handling
- **Integration Ready**: Works with any logging framework or crash reporting service

---

## 🔧 Design Principles

- **Unified Exception Handling**: Consistent `ExceptionEventArgs` interface across all exception sources
- **Resource Management**: Proper IDisposable implementation prevents memory leaks and event handler buildup
- **Performance Conscious**: Minimal overhead with efficient event handling and optional high-volume monitoring
- **Platform Agnostic**: Works across all .NET platforms with specialized MAUI support for mobile and desktop
- **Developer Friendly**: Simple API that follows .NET conventions and integrates easily with existing code
- **Production Ready**: Configurable logging levels and performance-optimized listeners for different environments

---

## 🚀 Installation

### For .NET Applications

```bash
dotnet add package Plugin.ExceptionListeners
```

### For MAUI Applications

```bash
dotnet add package Plugin.ExceptionListeners
dotnet add package Plugin.ExceptionListeners.Maui
```

---

## 🧪 Development

- **Testing**: xUnit test framework with comprehensive exception scenarios
- **Build**: `dotnet build` - Supports .NET 9.0
- **Test**: `dotnet test` - Includes platform-specific tests
- **MAUI Platforms**: iOS, Android, Windows, macOS Catalyst
- **Documentation**: DocFX-powered documentation with examples

---

## 📄 License

MIT — see `LICENSE.md` for details.

---

*Robust exception monitoring for .NET applications across all platforms and devices.*
