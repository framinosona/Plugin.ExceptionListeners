# Best Practices Guide

This guide outlines recommended practices for using Plugin.ExceptionListeners effectively and safely in production applications.

## General Principles

### 1. Choose the Right Listeners

Select exception listeners based on your specific needs:

```csharp
// Production recommendation - focus on critical exceptions
public void SetupProductionListeners()
{
    // Essential for crash prevention
    var unhandledListener = new CurrentDomainUnhandledExceptionListener(HandleCriticalException);

    // Important for async applications
    var taskListener = new TaskSchedulerUnobservedTaskExceptionListener(HandleAsyncException);

    // MAUI applications should include native handling
    #if __MOBILE__
    var nativeListener = new NativeUnhandledExceptionListener(HandleNativeException);
    #endif

    // FirstChanceExceptionListener - use carefully in production
    #if DEBUG
    var firstChanceListener = new CurrentDomainFirstChanceExceptionListener(HandleAllExceptions);
    #endif
}
```

### 2. Fail-Safe Exception Handlers

Never let exception handlers throw exceptions:

```csharp
// Good: Safe exception handling
private void HandleException(object? sender, ExceptionEventArgs e)
{
    try
    {
        // Your exception processing logic
        LogException(e.Exception);
        ReportException(e.Exception);
    }
    catch (Exception handlerException)
    {
        // Use fallback logging that can't fail
        System.Diagnostics.Debug.WriteLine($"Handler failed: {handlerException}");

        // Or use a simple file write
        try
        {
            File.AppendAllText("emergency.log",
                $"{DateTime.UtcNow}: Handler exception: {handlerException}\n");
        }
        catch
        {
            // Last resort - at least try console
            Console.WriteLine($"Emergency: {handlerException}");
        }
    }
}

// Bad: Handler that can throw
private void BadExceptionHandler(object? sender, ExceptionEventArgs e)
{
    // These operations can throw exceptions
    var config = ConfigurationService.GetConfig(); // Could throw
    var connection = DatabaseService.GetConnection(); // Could throw
    connection.LogException(e.Exception); // Could throw
}
```

### 3. Appropriate Logging Levels

Match logging levels to exception severity:

```csharp
private void HandleException(object? sender, ExceptionEventArgs e)
{
    var logLevel = DetermineLogLevel(e.Exception);

    _logger.Log(logLevel, e.Exception,
        "Exception caught by {ListenerType}: {ExceptionType}",
        sender?.GetType().Name ?? "Unknown",
        e.Exception.GetType().Name);
}

private LogLevel DetermineLogLevel(Exception exception)
{
    return exception switch
    {
        // Critical system exceptions
        OutOfMemoryException => LogLevel.Critical,
        StackOverflowException => LogLevel.Critical,
        AccessViolationException => LogLevel.Critical,

        // Important application exceptions
        UnauthorizedAccessException => LogLevel.Error,
        SecurityException => LogLevel.Error,

        // Network and I/O issues
        HttpRequestException => LogLevel.Warning,
        TimeoutException => LogLevel.Warning,
        IOException => LogLevel.Warning,

        // Expected exceptions that might be handled
        OperationCanceledException => LogLevel.Debug,
        TaskCanceledException => LogLevel.Debug,
        ObjectDisposedException => LogLevel.Debug,

        // Default to Information for monitoring
        _ => LogLevel.Information
    };
}
```

## Performance Best Practices

### 1. Lightweight First-Chance Handlers

First-chance exception handlers must be extremely fast:

```csharp
public class HighPerformanceFirstChanceHandler
{
    private static readonly ConcurrentDictionary<string, int> ExceptionCounts = new();
    private static long _totalExceptions = 0;

    public void HandleFirstChance(object? sender, ExceptionEventArgs e)
    {
        // Ultra-fast operations only
        Interlocked.Increment(ref _totalExceptions);

        // Quick counting without heavy string operations
        var typeHash = e.Exception.GetType().GetHashCode().ToString();
        ExceptionCounts.AddOrUpdate(typeHash, 1, (k, v) => v + 1);

        // Don't do any of these in first-chance handlers:
        // - File I/O
        // - Network calls
        // - Database operations
        // - Complex string formatting
        // - Reflection operations
        // - Async operations
    }

    // Report statistics periodically from a different thread
    public static void ReportStatistics()
    {
        Console.WriteLine($"Total exceptions: {_totalExceptions}");
        foreach (var kvp in ExceptionCounts.ToArray())
        {
            Console.WriteLine($"Type {kvp.Key}: {kvp.Value} occurrences");
        }
    }
}
```

### 2. Asynchronous Processing

Use background processing for heavy operations:

```csharp
public class AsynchronousExceptionHandler
{
    private readonly Channel<ExceptionInfo> _exceptionChannel;
    private readonly ChannelWriter<ExceptionInfo> _writer;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public AsynchronousExceptionHandler()
    {
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };

        _exceptionChannel = Channel.CreateBounded<ExceptionInfo>(options);
        _writer = _exceptionChannel.Writer;

        // Start background processing
        _ = Task.Run(ProcessExceptionsAsync);
    }

    public void HandleException(object? sender, ExceptionEventArgs e)
    {
        // Fast: just queue the exception
        var info = new ExceptionInfo
        {
            Exception = e.Exception,
            Timestamp = DateTimeOffset.UtcNow,
            Source = sender?.GetType().Name
        };

        if (!_writer.TryWrite(info))
        {
            // Channel is full - could implement fallback strategy
            System.Diagnostics.Debug.WriteLine("Exception queue full - dropping exception");
        }
    }

    private async Task ProcessExceptionsAsync()
    {
        await foreach (var info in _exceptionChannel.Reader.ReadAllAsync(_cancellationTokenSource.Token))
        {
            try
            {
                await ProcessSingleException(info);
            }
            catch (Exception ex)
            {
                // Don't let processing failures stop the processor
                System.Diagnostics.Debug.WriteLine($"Failed to process exception: {ex}");
            }
        }
    }

    private async Task ProcessSingleException(ExceptionInfo info)
    {
        // Heavy operations can be done here safely
        await LogToDatabase(info);
        await SendToMonitoringService(info);
        await UpdateMetrics(info);
    }

    private class ExceptionInfo
    {
        public Exception Exception { get; set; } = null!;
        public DateTimeOffset Timestamp { get; set; }
        public string? Source { get; set; }
    }
}
```

### 3. Memory Management

Prevent memory leaks in exception handling:

```csharp
public class MemoryEfficientExceptionHandler : IDisposable
{
    private readonly Timer _cleanupTimer;
    private readonly ConcurrentDictionary<string, ExceptionMetrics> _metrics = new();
    private volatile bool _disposed = false;

    public MemoryEfficientExceptionHandler()
    {
        // Periodic cleanup to prevent unbounded memory growth
        _cleanupTimer = new Timer(CleanupOldMetrics, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public void HandleException(object? sender, ExceptionEventArgs e)
    {
        if (_disposed) return;

        var key = CreateKey(e.Exception);

        _metrics.AddOrUpdate(key,
            new ExceptionMetrics { Count = 1, LastSeen = DateTimeOffset.UtcNow },
            (k, existing) => new ExceptionMetrics
            {
                Count = existing.Count + 1,
                LastSeen = DateTimeOffset.UtcNow
            });
    }

    private string CreateKey(Exception exception)
    {
        // Create a bounded key to prevent memory issues
        var type = exception.GetType().Name;
        var message = exception.Message?.Length > 100
            ? exception.Message.Substring(0, 100)
            : exception.Message ?? "";

        return $"{type}:{message.GetHashCode()}";
    }

    private void CleanupOldMetrics(object? state)
    {
        if (_disposed) return;

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-30);
        var keysToRemove = new List<string>();

        foreach (var kvp in _metrics)
        {
            if (kvp.Value.LastSeen < cutoff)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _metrics.TryRemove(key, out _);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _cleanupTimer?.Dispose();
        _metrics.Clear();
    }

    private class ExceptionMetrics
    {
        public int Count { get; set; }
        public DateTimeOffset LastSeen { get; set; }
    }
}
```

## Production Deployment Practices

### 1. Environment-Specific Configuration

Configure exception handling differently for different environments:

```csharp
public class EnvironmentAwareExceptionManager
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public void Initialize()
    {
        if (_environment.IsDevelopment())
        {
            SetupDevelopmentHandling();
        }
        else if (_environment.IsStaging())
        {
            SetupStagingHandling();
        }
        else
        {
            SetupProductionHandling();
        }
    }

    private void SetupDevelopmentHandling()
    {
        // Comprehensive logging for debugging
        new CurrentDomainFirstChanceExceptionListener(LogAllExceptionsDetailed);
        new CurrentDomainUnhandledExceptionListener(LogAndDebugUnhandled);
        new TaskSchedulerUnobservedTaskExceptionListener(LogAsyncExceptions);
    }

    private void SetupStagingHandling()
    {
        // Focus on unhandled exceptions, but include some monitoring
        new CurrentDomainUnhandledExceptionListener(LogAndReportUnhandled);
        new TaskSchedulerUnobservedTaskExceptionListener(LogAsyncExceptions);

        // Limited first-chance logging with filtering
        new CurrentDomainFirstChanceExceptionListener(LogFilteredFirstChance);
    }

    private void SetupProductionHandling()
    {
        // Minimal, focused exception handling
        new CurrentDomainUnhandledExceptionListener(ReportCriticalException);
        new TaskSchedulerUnobservedTaskExceptionListener(ReportAsyncException);

        // No first-chance exception logging in production
    }
}
```

### 2. Graceful Degradation

Implement fallback strategies for exception handling failures:

```csharp
public class ResilientExceptionHandler
{
    private readonly ILogger _primaryLogger;
    private readonly ILogger _fallbackLogger;
    private readonly string _emergencyLogPath;

    public void HandleException(object? sender, ExceptionEventArgs e)
    {
        // Multi-tier error handling

        // Tier 1: Primary logging
        if (TryLogToPrimary(e.Exception))
            return;

        // Tier 2: Fallback logging
        if (TryLogToFallback(e.Exception))
            return;

        // Tier 3: Emergency file logging
        if (TryLogToEmergencyFile(e.Exception))
            return;

        // Tier 4: System diagnostics (should always work)
        TryLogToSystemDiagnostics(e.Exception);
    }

    private bool TryLogToPrimary(Exception exception)
    {
        try
        {
            _primaryLogger.LogError(exception, "Exception occurred");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryLogToFallback(Exception exception)
    {
        try
        {
            _fallbackLogger.LogError(exception, "Exception occurred (fallback)");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryLogToEmergencyFile(Exception exception)
    {
        try
        {
            var message = $"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} - {exception}\n";
            File.AppendAllText(_emergencyLogPath, message);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void TryLogToSystemDiagnostics(Exception exception)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"EMERGENCY: {exception}");
            Console.WriteLine($"EMERGENCY: {exception}");
        }
        catch
        {
            // If even this fails, there's nothing more we can do
        }
    }
}
```

### 3. Security Considerations

Avoid logging sensitive information in exceptions:

```csharp
public class SecurityAwareExceptionHandler
{
    private readonly HashSet<string> _sensitivePatterns = new()
    {
        "password", "token", "secret", "key", "credential",
        "authorization", "authentication", "ssn", "social"
    };

    public void HandleException(object? sender, ExceptionEventArgs e)
    {
        var sanitizedException = SanitizeException(e.Exception);
        LogSanitizedException(sanitizedException);
    }

    private Exception SanitizeException(Exception original)
    {
        try
        {
            var message = SanitizeMessage(original.Message);
            var stackTrace = SanitizeStackTrace(original.StackTrace);

            // Create a sanitized version
            return new Exception($"[SANITIZED] {message}")
            {
                Source = original.Source
                // Don't copy Data dictionary as it might contain sensitive info
            };
        }
        catch
        {
            // If sanitization fails, return a generic exception
            return new Exception($"[SANITIZED] Exception of type {original.GetType().Name}");
        }
    }

    private string SanitizeMessage(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return "[No message]";

        var sanitized = message;

        // Remove potential sensitive data patterns
        foreach (var pattern in _sensitivePatterns)
        {
            if (sanitized.ToLower().Contains(pattern))
            {
                return "[Message contains sensitive data]";
            }
        }

        // Remove potential connection strings, URLs with credentials, etc.
        if (sanitized.Contains("://") && (sanitized.Contains("@") || sanitized.Contains("password")))
        {
            return "[Message contains potential credentials]";
        }

        return sanitized;
    }

    private string? SanitizeStackTrace(string? stackTrace)
    {
        // In production, you might want to remove or sanitize stack traces
        // that could reveal sensitive system information

        #if DEBUG
        return stackTrace; // Keep full stack trace in debug
        #else
        // In production, provide limited stack trace info
        if (string.IsNullOrEmpty(stackTrace))
            return null;

        var lines = stackTrace.Split('\n');
        if (lines.Length > 5)
        {
            // Only show top 5 stack frames
            return string.Join('\n', lines.Take(5)) + "\n[Additional frames omitted]";
        }

        return stackTrace;
        #endif
    }
}
```

## Monitoring and Alerting Best Practices

### 1. Structured Exception Metrics

Track exception patterns over time:

```csharp
public class ExceptionMetricsCollector
{
    private readonly IMetricsCollector _metrics;
    private readonly ConcurrentDictionary<string, ExceptionStats> _stats = new();

    public void HandleException(object? sender, ExceptionEventArgs e)
    {
        var exceptionType = e.Exception.GetType().Name;
        var source = sender?.GetType().Name ?? "Unknown";

        // Update metrics
        _metrics.IncrementCounter("exceptions.total");
        _metrics.IncrementCounter($"exceptions.by_type.{exceptionType}");
        _metrics.IncrementCounter($"exceptions.by_source.{source}");

        // Track patterns
        UpdateExceptionStats(exceptionType, e.Exception);

        // Check for anomalies
        CheckForAnomalies(exceptionType);
    }

    private void UpdateExceptionStats(string type, Exception exception)
    {
        _stats.AddOrUpdate(type,
            new ExceptionStats
            {
                Count = 1,
                FirstSeen = DateTimeOffset.UtcNow,
                LastSeen = DateTimeOffset.UtcNow,
                SampleMessage = exception.Message
            },
            (key, existing) => new ExceptionStats
            {
                Count = existing.Count + 1,
                FirstSeen = existing.FirstSeen,
                LastSeen = DateTimeOffset.UtcNow,
                SampleMessage = existing.SampleMessage // Keep original sample
            });
    }

    private void CheckForAnomalies(string exceptionType)
    {
        if (_stats.TryGetValue(exceptionType, out var stats))
        {
            // Alert on sudden spikes
            if (stats.Count > 100 &&
                DateTimeOffset.UtcNow - stats.FirstSeen < TimeSpan.FromMinutes(5))
            {
                _metrics.IncrementCounter("exceptions.anomaly.spike");
                // Trigger alert
            }
        }
    }

    private class ExceptionStats
    {
        public int Count { get; set; }
        public DateTimeOffset FirstSeen { get; set; }
        public DateTimeOffset LastSeen { get; set; }
        public string? SampleMessage { get; set; }
    }
}
```

### 2. Health Check Integration

Integrate exception monitoring with health checks:

```csharp
public class ExceptionHealthCheck : IHealthCheck
{
    private readonly ExceptionMetricsCollector _metricsCollector;
    private static long _recentUnhandledExceptions = 0;
    private static DateTimeOffset _lastUnhandledException = DateTimeOffset.MinValue;

    public static void RecordUnhandledException()
    {
        Interlocked.Increment(ref _recentUnhandledExceptions);
        _lastUnhandledException = DateTimeOffset.UtcNow;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var unhandledCount = Interlocked.Read(ref _recentUnhandledExceptions);
        var timeSinceLastUnhandled = DateTimeOffset.UtcNow - _lastUnhandledException;

        // Reset counter for next check
        Interlocked.Exchange(ref _recentUnhandledExceptions, 0);

        if (unhandledCount > 5)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"High number of unhandled exceptions: {unhandledCount}"));
        }

        if (timeSinceLastUnhandled < TimeSpan.FromMinutes(1) && unhandledCount > 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Recent unhandled exceptions: {unhandledCount}"));
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
```

## Testing Best Practices

### 1. Exception Handler Testing

Test your exception handlers thoroughly:

```csharp
public class ExceptionHandlerTests
{
    [Fact]
    public void ExceptionHandler_ShouldNotThrow_WhenHandlingException()
    {
        // Arrange
        var handler = new SafeExceptionHandler();
        var testException = new InvalidOperationException("Test exception");

        // Act & Assert - handler should never throw
        var exception = Record.Exception(() =>
            handler.HandleException(this, new ExceptionEventArgs(testException)));

        Assert.Null(exception);
    }

    [Fact]
    public void ExceptionHandler_ShouldHandleNullSender()
    {
        // Arrange
        var handler = new SafeExceptionHandler();
        var testException = new ArgumentException("Test");

        // Act & Assert
        var exception = Record.Exception(() =>
            handler.HandleException(null, new ExceptionEventArgs(testException)));

        Assert.Null(exception);
    }

    [Fact]
    public void ExceptionHandler_ShouldHandleRecursiveExceptions()
    {
        // Test that handlers don't create infinite loops
        var handler = new SafeExceptionHandler();
        var innerEx = new InvalidOperationException("Inner");
        var outerEx = new Exception("Outer", innerEx);

        var exception = Record.Exception(() =>
            handler.HandleException(this, new ExceptionEventArgs(outerEx)));

        Assert.Null(exception);
    }
}
```

### 2. Integration Testing

Test exception listeners in integration scenarios:

```csharp
public class ExceptionListenerIntegrationTests
{
    [Fact]
    public async Task UnhandledExceptionListener_ShouldCatchAsyncExceptions()
    {
        // Arrange
        var caughtException = false;
        var listener = new CurrentDomainUnhandledExceptionListener((sender, e) =>
        {
            caughtException = true;
        });

        try
        {
            // Act - create unhandled exception scenario
            // Note: This is tricky to test safely without crashing the test process
            // Consider using separate app domains or processes for this type of testing

            await Task.Delay(100); // Placeholder for actual test

            // Assert
            Assert.True(true); // Placeholder
        }
        finally
        {
            listener.Dispose();
        }
    }
}
```

## Common Anti-Patterns to Avoid

### 1. Ignoring Exception Context

```csharp
// Bad: Losing important context
private void BadHandler(object? sender, ExceptionEventArgs e)
{
    Logger.LogError("An exception occurred");
}

// Good: Preserving context
private void GoodHandler(object? sender, ExceptionEventArgs e)
{
    Logger.LogError(e.Exception,
        "Exception in {Source}: {ExceptionType} - {Message}",
        sender?.GetType().Name ?? "Unknown",
        e.Exception.GetType().Name,
        e.Exception.Message);
}
```

### 2. Blocking Operations in High-Frequency Handlers

```csharp
// Bad: Blocking operations in first-chance handler
private void BadFirstChanceHandler(object? sender, ExceptionEventArgs e)
{
    // These operations are too slow for first-chance handlers
    File.WriteAllText("log.txt", e.Exception.ToString());
    HttpClient.PostAsync("http://api.example.com/log", content).Wait();
    Database.LogException(e.Exception);
}

// Good: Minimal processing
private void GoodFirstChanceHandler(object? sender, ExceptionEventArgs e)
{
    // Fast operations only
    Interlocked.Increment(ref _exceptionCount);
    _exceptionQueue.TryAdd(e.Exception);
}
```

### 3. Resource Leaks

```csharp
// Bad: Not disposing listeners
public class BadService
{
    public void Initialize()
    {
        var listener = new CurrentDomainUnhandledExceptionListener(HandleException);
        // Listener is never disposed - memory leak!
    }
}

// Good: Proper resource management
public class GoodService : IDisposable
{
    private readonly List<IDisposable> _listeners = new();

    public void Initialize()
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

By following these best practices, you'll have robust, performant, and maintainable exception handling in your applications.
