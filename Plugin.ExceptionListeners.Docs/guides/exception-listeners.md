# Exception Listeners Guide

This guide covers the different types of exception listeners available in Plugin.ExceptionListeners and when to use each one.

## Overview

Plugin.ExceptionListeners provides four main types of exception listeners:

1. **FirstChanceExceptionListener** - Catches all exceptions as they occur
2. **UnhandledExceptionListener** - Catches exceptions that would terminate the application
3. **UnobservedTaskExceptionListener** - Catches exceptions from unobserved Tasks
4. **NativeUnhandledExceptionListener** - Catches native platform exceptions (MAUI only)

## CurrentDomainFirstChanceExceptionListener

### Purpose

Captures all exceptions that occur in the application domain, regardless of whether they are handled or not. This listener fires **before** any exception handling code runs.

### Use Cases

- **Comprehensive monitoring** - Track all exceptions for diagnostics
- **Performance analysis** - Identify frequently thrown exceptions
- **Debugging** - Catch exceptions that might be silently handled
- **Metrics collection** - Gather exception statistics

### Characteristics

- **High frequency** - Can trigger thousands of times per second in busy applications
- **Performance sensitive** - Handlers must be extremely fast
- **Informational** - Exceptions may be handled normally after the event
- **All exceptions** - Includes both handled and unhandled exceptions

### Example Usage

```csharp
using var listener = new CurrentDomainFirstChanceExceptionListener(HandleFirstChance);

private void HandleFirstChance(object? sender, ExceptionEventArgs e)
{
    // Keep this handler VERY fast - no heavy operations!

    // Good: Simple counting or filtering
    if (e.Exception is OutOfMemoryException)
    {
        Interlocked.Increment(ref _outOfMemoryCount);
    }

    // Bad: Heavy operations that could impact performance
    // await SomeAsyncOperation(e.Exception); // DON'T DO THIS
    // File.WriteAllText("log.txt", e.Exception.ToString()); // DON'T DO THIS
}
```

### Best Practices

```csharp
public class FirstChanceMonitor
{
    private readonly ConcurrentDictionary<string, int> _exceptionCounts = new();
    private readonly Timer _reportingTimer;

    public FirstChanceMonitor()
    {
        var listener = new CurrentDomainFirstChanceExceptionListener(HandleFirstChance);

        // Report statistics periodically rather than on each exception
        _reportingTimer = new Timer(ReportStatistics, null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    private void HandleFirstChance(object? sender, ExceptionEventArgs e)
    {
        // Very fast - just increment counters
        var key = e.Exception.GetType().Name;
        _exceptionCounts.AddOrUpdate(key, 1, (k, v) => v + 1);
    }

    private void ReportStatistics(object? state)
    {
        var snapshot = _exceptionCounts.ToArray();
        foreach (var (type, count) in snapshot)
        {
            Console.WriteLine($"{type}: {count} occurrences");
        }
    }
}
```

## CurrentDomainUnhandledExceptionListener

### What It Does

Captures exceptions that are not handled by application code and would normally terminate the application.

### When to Use

- **Crash prevention** - Log critical errors before app termination
- **Error reporting** - Send crash reports to monitoring services
- **Graceful shutdown** - Perform cleanup before termination
- **User notification** - Show error messages to users

### Key Features

- **Critical errors** - Usually indicates serious application problems
- **Application termination** - App may still terminate after handling
- **Low frequency** - Should be rare in well-written applications
- **Last chance** - Final opportunity to handle the error

### Implementation Example

```csharp
using var listener = new CurrentDomainUnhandledExceptionListener(HandleUnhandled);

private void HandleUnhandled(object? sender, ExceptionEventArgs e)
{
    // This is a critical error - the application may terminate

    try
    {
        // Log the critical error
        _logger.LogCritical(e.Exception, "Unhandled exception occurred");

        // Send crash report
        await CrashReportingService.SendReport(e.Exception);

        // Attempt graceful cleanup
        await PerformEmergencyCleanup();

        // Notify user if possible
        ShowCriticalErrorDialog(e.Exception.Message);
    }
    catch
    {
        // Don't let exception handling fail
        System.Diagnostics.Debug.WriteLine($"Failed to handle unhandled exception: {e.Exception}");
    }
}
```

### Error Recovery Patterns

```csharp
public class UnhandledExceptionManager
{
    private readonly ILogger _logger;
    private readonly ICrashReporter _crashReporter;
    private int _unhandledCount = 0;

    public void HandleUnhandledException(object? sender, ExceptionEventArgs e)
    {
        var count = Interlocked.Increment(ref _unhandledCount);

        try
        {
            // Always log
            _logger.LogCritical(e.Exception,
                "Unhandled exception #{Count}", count);

            // Categorize and handle appropriately
            switch (e.Exception)
            {
                case OutOfMemoryException:
                    HandleOutOfMemoryException(e.Exception);
                    break;

                case StackOverflowException:
                    HandleStackOverflowException(e.Exception);
                    break;

                case AccessViolationException:
                    HandleAccessViolationException(e.Exception);
                    break;

                default:
                    HandleGenericUnhandledException(e.Exception);
                    break;
            }

            // Send crash report
            _ = Task.Run(async () =>
            {
                try
                {
                    await _crashReporter.ReportCrash(e.Exception);
                }
                catch
                {
                    // Ignore crash reporting failures
                }
            });
        }
        catch (Exception handlerException)
        {
            // Log handler failure if possible
            System.Diagnostics.Debug.WriteLine(
                $"Exception handler failed: {handlerException}");
        }
    }

    private void HandleOutOfMemoryException(Exception ex)
    {
        // Try to free memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Disable non-essential features
        DisableNonEssentialFeatures();
    }

    private void HandleStackOverflowException(Exception ex)
    {
        // Can't do much for stack overflow - just log
        System.Diagnostics.Debug.WriteLine("Stack overflow detected");
    }

    private void HandleAccessViolationException(Exception ex)
    {
        // Memory corruption - immediate shutdown recommended
        System.Diagnostics.Debug.WriteLine("Access violation - memory corruption possible");
    }

    private void HandleGenericUnhandledException(Exception ex)
    {
        // Standard unhandled exception processing
        _logger.LogError(ex, "Generic unhandled exception");
    }

    private void DisableNonEssentialFeatures()
    {
        // Implementation depends on your application
    }
}
```

## TaskSchedulerUnobservedTaskExceptionListener

### What It Does

Captures exceptions from Tasks that complete with errors but are never observed (awaited or accessed via `.Result`).

### When to Use

- **Async debugging** - Find fire-and-forget tasks with errors
- **Resource leak prevention** - Identify tasks that fail silently
- **Background task monitoring** - Track background operation failures
- **Data integrity** - Ensure async operations complete successfully

### Key Features

- **Delayed detection** - Triggered during garbage collection
- **Async-related** - Only affects Task-based operations
- **Silent failures** - These exceptions would otherwise be lost
- **Automatic observation** - Listener marks exceptions as observed

### Implementation Example

```csharp
using var listener = new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException);

private void HandleTaskException(object? sender, ExceptionEventArgs e)
{
    // This task failed but was never awaited
    _logger.LogError(e.Exception,
        "Unobserved task exception - potential fire-and-forget task failure");

    // The exception is automatically marked as observed by the listener
    // so it won't crash the application
}
```

### Common Scenarios

```csharp
public class TaskExceptionExamples
{
    private readonly TaskSchedulerUnobservedTaskExceptionListener _listener;

    public TaskExceptionExamples()
    {
        _listener = new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException);
    }

    public void FireAndForgetExample()
    {
        // This task will fail, but the exception won't be observed
        Task.Run(async () =>
        {
            await Task.Delay(100);
            throw new InvalidOperationException("This will become unobserved");
        });

        // Not awaiting the task means its exception becomes unobserved
        // The TaskSchedulerUnobservedTaskExceptionListener will catch it
    }

    public async Task ProperAsyncExample()
    {
        try
        {
            // Properly awaited - exceptions are observed normally
            await Task.Run(async () =>
            {
                await Task.Delay(100);
                throw new InvalidOperationException("This will be caught normally");
            });
        }
        catch (InvalidOperationException ex)
        {
            // Normal exception handling - no unobserved task exception
            Console.WriteLine($"Caught exception: {ex.Message}");
        }
    }

    public void ConfigureAwaitExample()
    {
        // Be careful with ConfigureAwait(false) and fire-and-forget
        Task.Run(async () =>
        {
            await SomeAsyncOperation().ConfigureAwait(false);
            // If SomeAsyncOperation throws and this task isn't awaited,
            // it becomes an unobserved task exception
        });
    }

    private async Task SomeAsyncOperation()
    {
        await Task.Delay(100);
        throw new InvalidOperationException("Simulated failure");
    }

    private void HandleTaskException(object? sender, ExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(
            $"Unobserved task exception: {e.Exception.GetType().Name}: {e.Exception.Message}");

        // Log stack trace to help identify the source
        System.Diagnostics.Debug.WriteLine($"Stack trace: {e.Exception.StackTrace}");
    }
}
```

## NativeUnhandledExceptionListener (MAUI Only)

### Functionality

Captures unhandled exceptions that originate from native platform code (iOS Objective-C, Android Java, Windows native, etc.).

### Usage Scenarios

- **Cross-platform error handling** - Unified handling across all platforms
- **Native API failures** - Catch platform-specific API errors
- **Third-party library errors** - Handle native library failures
- **Platform permission errors** - Catch permission-related native errors

### Platform Support

| Platform | Native Exception Sources |
|----------|-------------------------|
| **iOS/macOS** | Objective-C runtime exceptions, CoreFoundation errors |
| **Android** | Java runtime exceptions, JNI errors |
| **Windows** | COM exceptions, Win32 errors, WinRT errors |

### Usage Example

```csharp
// In App.xaml.cs or similar MAUI setup
using var nativeListener = new NativeUnhandledExceptionListener(HandleNativeException);

private void HandleNativeException(object? sender, ExceptionEventArgs e)
{
    // Handle platform-specific exceptions

    if (e.Exception is NativeUnhandledException nativeEx)
    {
        // This is a wrapped native exception
        _logger.LogError(nativeEx,
            "Native platform exception: {Message}",
            nativeEx.Message);

        // Check for inner exception with more details
        if (nativeEx.InnerException != null)
        {
            _logger.LogError(nativeEx.InnerException,
                "Original native exception details");
        }
    }
    else
    {
        // Other types of exceptions that came through native channels
        _logger.LogError(e.Exception,
            "Exception from native platform");
    }
}
```

### Platform-Specific Handling

```csharp
public class PlatformSpecificNativeHandler
{
    public void HandleNativeException(object? sender, ExceptionEventArgs e)
    {
        var platformInfo = GetPlatformInfo();

        _logger.LogError(e.Exception,
            "Native exception on {Platform}: {Exception}",
            platformInfo, e.Exception.Message);

#if ANDROID
        HandleAndroidNativeException(e.Exception);
#elif IOS
        HandleiOSNativeException(e.Exception);
#elif WINDOWS
        HandleWindowsNativeException(e.Exception);
#elif MACCATALYST
        HandleMacCatalystNativeException(e.Exception);
#endif
    }

#if ANDROID
    private void HandleAndroidNativeException(Exception ex)
    {
        // Android-specific native exception handling
        if (ex.Message?.Contains("Java.Lang") == true)
        {
            // Java runtime exception
            _logger.LogWarning("Java runtime exception: {Message}", ex.Message);
        }
        else if (ex.Message?.Contains("JNI") == true)
        {
            // JNI-related error
            _logger.LogError("JNI error: {Message}", ex.Message);
        }
    }
#endif

#if IOS
    private void HandleiOSNativeException(Exception ex)
    {
        // iOS-specific native exception handling
        if (ex is ObjCException objcEx)
        {
            _logger.LogError("Objective-C exception: {Name} - {Reason}",
                objcEx.Name, objcEx.Reason);
        }
    }
#endif

#if WINDOWS
    private void HandleWindowsNativeException(Exception ex)
    {
        // Windows-specific native exception handling
        if (ex.Message?.Contains("HRESULT") == true)
        {
            _logger.LogError("Windows COM/WinRT error: {Message}", ex.Message);
        }
    }
#endif

    private string GetPlatformInfo()
    {
#if ANDROID
        return $"Android {Android.OS.Build.VERSION.Release}";
#elif IOS
        return $"iOS {UIKit.UIDevice.CurrentDevice.SystemVersion}";
#elif WINDOWS
        return $"Windows {Environment.OSVersion.Version}";
#elif MACCATALYST
        return $"Mac Catalyst {Foundation.NSProcessInfo.ProcessInfo.OperatingSystemVersion}";
#else
        return "Unknown Platform";
#endif
    }
}
```

## Combining Listeners

### Comprehensive Exception Monitoring

```csharp
public class ComprehensiveExceptionMonitor : IDisposable
{
    private readonly List<ExceptionListener> _listeners = new();
    private readonly ILogger _logger;

    public ComprehensiveExceptionMonitor(ILogger logger)
    {
        _logger = logger;
        SetupListeners();
    }

    private void SetupListeners()
    {
        // Monitor all exceptions (high frequency)
        _listeners.Add(new CurrentDomainFirstChanceExceptionListener(HandleFirstChance));

        // Monitor critical failures
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleUnhandled));

        // Monitor async failures
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException));

#if __MOBILE__
        // Monitor native platform failures (MAUI only)
        _listeners.Add(new NativeUnhandledExceptionListener(HandleNativeException));
#endif
    }

    private void HandleFirstChance(object? sender, ExceptionEventArgs e)
    {
        // High-frequency monitoring - keep lightweight
        MetricsCollector.IncrementExceptionCounter(e.Exception.GetType().Name);
    }

    private void HandleUnhandled(object? sender, ExceptionEventArgs e)
    {
        _logger.LogCritical(e.Exception, "CRITICAL: Unhandled exception");

        // Emergency procedures
        _ = Task.Run(async () =>
        {
            try
            {
                await CrashReporter.ReportCriticalError(e.Exception);
                await EmergencyDataSave();
            }
            catch
            {
                // Ignore failures in emergency procedures
            }
        });
    }

    private void HandleTaskException(object? sender, ExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unobserved task exception");

        // Check if this indicates a systematic problem
        if (IsSystematicAsyncFailure(e.Exception))
        {
            _logger.LogWarning("Systematic async failure pattern detected");
        }
    }

#if __MOBILE__
    private void HandleNativeException(object? sender, ExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Native platform exception");

        // Platform-specific recovery
        PlatformRecoveryService.AttemptRecovery(e.Exception);
    }
#endif

    private bool IsSystematicAsyncFailure(Exception ex)
    {
        // Implementation depends on your application's async patterns
        return false;
    }

    private async Task EmergencyDataSave()
    {
        // Implement emergency data persistence
        await Task.CompletedTask;
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

## Performance Considerations

### Listener Impact

| Listener Type | Performance Impact | Frequency | Recommendations |
|--------------|-------------------|-----------|----------------|
| FirstChance | **High** | Very High | Keep handlers extremely fast |
| Unhandled | Low | Very Low | Can perform comprehensive logging |
| Task | Medium | Low-Medium | Moderate processing acceptable |
| Native | Low | Low | Platform-specific handling OK |

### Optimization Strategies

```csharp
public class OptimizedExceptionHandler
{
    private readonly ConcurrentQueue<ExceptionInfo> _exceptionQueue = new();
    private readonly Timer _processingTimer;
    private readonly SemaphoreSlim _processingSemaphore = new(1);

    public OptimizedExceptionHandler()
    {
        // Process exceptions asynchronously to avoid blocking
        _processingTimer = new Timer(ProcessQueuedExceptions, null,
            TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
    }

    public void HandleException(object? sender, ExceptionEventArgs e)
    {
        // Extremely fast - just queue for later processing
        _exceptionQueue.Enqueue(new ExceptionInfo
        {
            Exception = e.Exception,
            Timestamp = DateTime.UtcNow,
            Source = sender?.GetType().Name
        });
    }

    private async void ProcessQueuedExceptions(object? state)
    {
        if (!await _processingSemaphore.WaitAsync(100)) return;

        try
        {
            var exceptions = new List<ExceptionInfo>();

            // Drain the queue
            while (_exceptionQueue.TryDequeue(out var info) && exceptions.Count < 50)
            {
                exceptions.Add(info);
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

    private async Task ProcessExceptionBatch(List<ExceptionInfo> exceptions)
    {
        // Group similar exceptions for efficient processing
        var grouped = exceptions
            .GroupBy(e => new { Type = e.Exception.GetType(), Message = e.Exception.Message })
            .ToList();

        foreach (var group in grouped)
        {
            var count = group.Count();
            var first = group.First();

            if (count > 1)
            {
                Console.WriteLine($"{count}x {first.Exception.GetType().Name}: {first.Exception.Message}");
            }
            else
            {
                Console.WriteLine($"{first.Exception.GetType().Name}: {first.Exception.Message}");
            }
        }
    }

    private class ExceptionInfo
    {
        public Exception Exception { get; set; } = null!;
        public DateTime Timestamp { get; set; }
        public string? Source { get; set; }
    }
}
```

This guide should give you a comprehensive understanding of when and how to use each type of exception listener effectively.
