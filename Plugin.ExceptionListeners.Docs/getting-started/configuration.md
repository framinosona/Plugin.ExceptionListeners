# Configuration Guide

Learn how to configure Plugin.ExceptionListeners for different scenarios and requirements.

## Listener Configuration

### Basic Listener Setup

Each exception listener can be configured independently:

```csharp
// Basic configuration - handle all exceptions
var listener = new CurrentDomainFirstChanceExceptionListener(HandleException);

// Custom configuration with filtering
var listener = new CurrentDomainFirstChanceExceptionListener(e => {
    if (ShouldHandle(e.Exception))
    {
        HandleException(e);
    }
});
```

### Listener Lifetime Management

#### Using Statements (Recommended)

```csharp
using var listener = new CurrentDomainFirstChanceExceptionListener(HandleException);
// Listener is automatically disposed when out of scope
```

#### Manual Disposal

```csharp
var listener = new CurrentDomainFirstChanceExceptionListener(HandleException);

try
{
    // Application code
}
finally
{
    listener.Dispose(); // Manual cleanup
}
```

#### Dependency Injection

```csharp
// In your DI container setup
services.AddSingleton<IExceptionHandler, ExceptionHandler>();
services.AddSingleton(provider =>
    new CurrentDomainUnhandledExceptionListener(
        provider.GetService<IExceptionHandler>()!.HandleException
    ));

// Service implementation
public interface IExceptionHandler
{
    void HandleException(object? sender, ExceptionEventArgs e);
}

public class ExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ExceptionHandler> _logger;

    public ExceptionHandler(ILogger<ExceptionHandler> logger)
    {
        _logger = logger;
    }

    public void HandleException(object? sender, ExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Exception caught by listener");
    }
}
```

## Exception Filtering

### Type-Based Filtering

```csharp
private void HandleException(object? sender, ExceptionEventArgs e)
{
    switch (e.Exception)
    {
        case OperationCanceledException:
            // Ignore cancellation exceptions
            return;

        case HttpRequestException httpEx:
            HandleNetworkException(httpEx);
            break;

        case UnauthorizedAccessException authEx:
            HandleAuthorizationException(authEx);
            break;

        default:
            HandleGenericException(e.Exception);
            break;
    }
}
```

### Severity-Based Filtering

```csharp
private readonly HashSet<Type> _ignoredTypes = new()
{
    typeof(OperationCanceledException),
    typeof(TaskCanceledException),
    typeof(ObjectDisposedException)
};

private void HandleException(object? sender, ExceptionEventArgs e)
{
    // Skip known benign exceptions
    if (_ignoredTypes.Contains(e.Exception.GetType()))
        return;

    // Handle based on severity
    if (IsCritical(e.Exception))
    {
        HandleCriticalException(e.Exception);
    }
    else if (IsWarning(e.Exception))
    {
        HandleWarningException(e.Exception);
    }
    else
    {
        HandleInfoException(e.Exception);
    }
}

private bool IsCritical(Exception ex) =>
    ex is OutOfMemoryException ||
    ex is StackOverflowException ||
    ex is AccessViolationException;

private bool IsWarning(Exception ex) =>
    ex is TimeoutException ||
    ex is HttpRequestException ||
    ex is SocketException;
```

## Performance Configuration

### High-Performance Scenarios

When dealing with high exception volumes (especially first-chance exceptions):

```csharp
public class HighPerformanceExceptionHandler
{
    private readonly ConcurrentQueue<Exception> _exceptionQueue = new();
    private readonly Timer _processingTimer;
    private readonly SemaphoreSlim _processingSemaphore = new(1);

    public HighPerformanceExceptionHandler()
    {
        // Process exceptions in batches to avoid performance impact
        _processingTimer = new Timer(ProcessExceptions, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void HandleException(object? sender, ExceptionEventArgs e)
    {
        // Quickly queue the exception without blocking
        _exceptionQueue.Enqueue(e.Exception);
    }

    private async void ProcessExceptions(object? state)
    {
        if (!await _processingSemaphore.WaitAsync(100)) return;

        try
        {
            var exceptions = new List<Exception>();

            // Drain the queue
            while (_exceptionQueue.TryDequeue(out var exception) && exceptions.Count < 100)
            {
                exceptions.Add(exception);
            }

            if (exceptions.Count > 0)
            {
                await ProcessExceptionBatch(exceptions);
            }
        }
        finally
        {
            _processingSemaphore.Release();
        }
    }

    private async Task ProcessExceptionBatch(List<Exception> exceptions)
    {
        // Group by type for efficient processing
        var grouped = exceptions.GroupBy(e => e.GetType().Name);

        foreach (var group in grouped)
        {
            // Process similar exceptions together
            await ProcessSimilarExceptions(group.Key, group.ToList());
        }
    }

    private async Task ProcessSimilarExceptions(string typeName, List<Exception> exceptions)
    {
        // Implement batch processing logic
        await Task.CompletedTask;
    }
}
```

### Rate Limiting

```csharp
public class RateLimitedExceptionHandler
{
    private readonly Dictionary<string, DateTime> _lastSeen = new();
    private readonly TimeSpan _minInterval = TimeSpan.FromSeconds(5);
    private readonly object _lock = new();

    public void HandleException(object? sender, ExceptionEventArgs e)
    {
        var key = $"{e.Exception.GetType().Name}:{e.Exception.Message}";

        lock (_lock)
        {
            if (_lastSeen.TryGetValue(key, out var lastTime) &&
                DateTime.UtcNow - lastTime < _minInterval)
            {
                return; // Skip duplicate exception within time window
            }

            _lastSeen[key] = DateTime.UtcNow;
        }

        ProcessException(e.Exception);
    }

    private void ProcessException(Exception exception)
    {
        // Process unique exceptions
        Console.WriteLine($"Processing: {exception.GetType().Name}: {exception.Message}");
    }
}
```

## MAUI-Specific Configuration

### Platform-Specific Handling

```csharp
public class PlatformSpecificHandler
{
    public void HandleNativeException(object? sender, ExceptionEventArgs e)
    {
#if ANDROID
        if (e.Exception.Message?.Contains("Java.Lang") == true)
        {
            HandleAndroidException(e.Exception);
            return;
        }
#endif

#if IOS
        if (e.Exception is ObjCException objcEx)
        {
            HandleiOSException(objcEx);
            return;
        }
#endif

#if WINDOWS
        if (e.Exception.Message?.Contains("HRESULT") == true)
        {
            HandleWindowsException(e.Exception);
            return;
        }
#endif

        HandleGenericNativeException(e.Exception);
    }

#if ANDROID
    private void HandleAndroidException(Exception ex)
    {
        // Android-specific handling
        System.Diagnostics.Debug.WriteLine($"Android exception: {ex.Message}");
    }
#endif

#if IOS
    private void HandleiOSException(ObjCException ex)
    {
        // iOS-specific handling
        System.Diagnostics.Debug.WriteLine($"iOS exception: {ex.Message}");
    }
#endif

#if WINDOWS
    private void HandleWindowsException(Exception ex)
    {
        // Windows-specific handling
        System.Diagnostics.Debug.WriteLine($"Windows exception: {ex.Message}");
    }
#endif

    private void HandleGenericNativeException(Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Generic native exception: {ex.Message}");
    }
}
```

### Lifecycle Integration

```csharp
public partial class App : Application
{
    private ExceptionManager? _exceptionManager;

    public App()
    {
        InitializeComponent();

        // Initialize after other setup
        _exceptionManager = new ExceptionManager();

        MainPage = new AppShell();
    }

    protected override void OnStart()
    {
        _exceptionManager?.Start();
        base.OnStart();
    }

    protected override void OnSleep()
    {
        _exceptionManager?.Pause();
        base.OnSleep();
    }

    protected override void OnResume()
    {
        _exceptionManager?.Resume();
        base.OnResume();
    }

    protected override void OnTerminate()
    {
        _exceptionManager?.Stop();
        _exceptionManager?.Dispose();
        base.OnTerminate();
    }
}

public class ExceptionManager : IDisposable
{
    private readonly List<ExceptionListener> _listeners = new();
    private bool _isStarted;

    public void Start()
    {
        if (_isStarted) return;

        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleException));
        _listeners.Add(new NativeUnhandledExceptionListener(HandleNativeException));

        _isStarted = true;
    }

    public void Pause()
    {
        // Optionally pause processing
    }

    public void Resume()
    {
        // Resume if paused
    }

    public void Stop()
    {
        foreach (var listener in _listeners)
        {
            listener.Dispose();
        }
        _listeners.Clear();
        _isStarted = false;
    }

    public void Dispose() => Stop();

    private void HandleException(object? sender, ExceptionEventArgs e) { /* ... */ }
    private void HandleNativeException(object? sender, ExceptionEventArgs e) { /* ... */ }
}
```

## Logging Integration

### Microsoft.Extensions.Logging

```csharp
public class LoggingExceptionHandler
{
    private readonly ILogger<LoggingExceptionHandler> _logger;

    public LoggingExceptionHandler(ILogger<LoggingExceptionHandler> logger)
    {
        _logger = logger;
    }

    public void HandleException(object? sender, ExceptionEventArgs e)
    {
        var logLevel = DetermineLogLevel(e.Exception);

        _logger.Log(logLevel, e.Exception,
            "Exception caught by listener. Source: {Source}, Type: {ExceptionType}",
            sender?.GetType().Name ?? "Unknown",
            e.Exception.GetType().Name);
    }

    private LogLevel DetermineLogLevel(Exception exception) =>
        exception switch
        {
            OperationCanceledException => LogLevel.Debug,
            TimeoutException => LogLevel.Warning,
            OutOfMemoryException => LogLevel.Critical,
            _ => LogLevel.Error
        };
}
```

### Serilog

```csharp
public class SerilogExceptionHandler
{
    private readonly ILogger _logger = Log.ForContext<SerilogExceptionHandler>();

    public void HandleException(object? sender, ExceptionEventArgs e)
    {
        _logger
            .ForContext("Source", sender?.GetType().Name)
            .ForContext("ExceptionType", e.Exception.GetType().Name)
            .ForContext("StackTrace", e.Exception.StackTrace)
            .Error(e.Exception, "Exception caught by listener");
    }
}
```

## Configuration Best Practices

### 1. Choose Appropriate Listeners

- **First-chance**: Use for comprehensive monitoring, but be aware of performance impact
- **Unhandled**: Essential for crash prevention and critical error handling
- **Task**: Important for async-heavy applications
- **Native**: Required for MAUI applications dealing with platform-specific code

### 2. Implement Proper Filtering

```csharp
// Good: Selective handling
private void HandleException(object? sender, ExceptionEventArgs e)
{
    if (ShouldIgnore(e.Exception)) return;

    ProcessException(e.Exception);
}

// Bad: Handling everything without discrimination
private void HandleException(object? sender, ExceptionEventArgs e)
{
    // This could overwhelm your logging system
    _logger.LogError(e.Exception, "Exception occurred");
}
```

### 3. Avoid Throwing in Handlers

```csharp
// Good: Safe exception handling
private void HandleException(object? sender, ExceptionEventArgs e)
{
    try
    {
        ProcessException(e.Exception);
    }
    catch
    {
        // Don't let handler exceptions escape
    }
}

// Bad: Throwing exceptions in handlers
private void HandleException(object? sender, ExceptionEventArgs e)
{
    if (e.Exception == null)
        throw new ArgumentException("Exception cannot be null"); // Could cause recursion
}
```

### 4. Manage Listener Lifetime

```csharp
// Good: Proper cleanup
public class Service : IDisposable
{
    private readonly List<IDisposable> _listeners = new();

    public Service()
    {
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleException));
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

## Environment-Specific Configuration

### Development vs Production

```csharp
public class EnvironmentAwareHandler
{
    private readonly bool _isDevelopment;

    public EnvironmentAwareHandler(IWebHostEnvironment env)
    {
        _isDevelopment = env.IsDevelopment();
    }

    public void HandleException(object? sender, ExceptionEventArgs e)
    {
        if (_isDevelopment)
        {
            // Detailed logging for development
            Console.WriteLine($"Full exception details: {e.Exception}");
        }
        else
        {
            // Minimal logging for production
            Console.WriteLine($"Exception: {e.Exception.GetType().Name}");
        }
    }
}
```

This configuration guide should help you set up Plugin.ExceptionListeners optimally for your specific needs and environment.
