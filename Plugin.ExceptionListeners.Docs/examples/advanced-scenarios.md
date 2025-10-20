# Advanced Scenarios

This section covers advanced use cases and integration patterns for Plugin.ExceptionListeners.

## Enterprise Logging Integration

### Serilog Integration with Structured Logging

```csharp
using Serilog;
using Serilog.Events;
using Plugin.ExceptionListeners;
using Plugin.ExceptionListeners.Listeners;

public class EnterpriseLoggingService
{
    private readonly ILogger _logger;
    private readonly List<ExceptionListener> _listeners = new();

    public EnterpriseLoggingService()
    {
        // Configure Serilog with structured logging
        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.WithProperty("Application", "MyApp")
            .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"))
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.File("logs/app-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .WriteTo.Seq("http://localhost:5341") // Seq for structured log analysis
            .CreateLogger();

        Log.Logger = _logger;
    }

    public void Initialize()
    {
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleUnhandledException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException));

        _logger.Information("Enterprise exception handling initialized");
    }

    private void HandleUnhandledException(object? sender, ExceptionEventArgs e)
    {
        // Rich structured logging with context
        _logger
            .ForContext("ExceptionSource", sender?.GetType().Name)
            .ForContext("ExceptionType", e.Exception.GetType().Name)
            .ForContext("ExceptionData", e.Exception.Data, destructureObjects: true)
            .ForContext("StackTrace", e.Exception.StackTrace)
            .ForContext("InnerException", e.Exception.InnerException?.ToString())
            .Fatal(e.Exception, "Unhandled exception occurred in {Source}",
                sender?.GetType().Name ?? "Unknown");

        // Send critical alert
        SendCriticalAlert(e.Exception);
    }

    private void HandleTaskException(object? sender, ExceptionEventArgs e)
    {
        _logger
            .ForContext("ExceptionSource", "TaskScheduler")
            .ForContext("ExceptionType", e.Exception.GetType().Name)
            .Error(e.Exception, "Unobserved task exception");
    }

    private async void SendCriticalAlert(Exception exception)
    {
        try
        {
            // Example: Send to Slack, Teams, or email
            var alertService = new AlertService();
            await alertService.SendCriticalAlert(
                title: "Critical Application Error",
                message: $"Unhandled exception: {exception.GetType().Name}",
                details: exception.ToString());
        }
        catch (Exception alertEx)
        {
            _logger.Error(alertEx, "Failed to send critical alert");
        }
    }

    public void Dispose()
    {
        foreach (var listener in _listeners)
        {
            listener.Dispose();
        }
        _listeners.Clear();
        _logger.Information("Enterprise exception handling disposed");
    }
}

// Alert service for critical notifications
public class AlertService
{
    private readonly HttpClient _httpClient = new();

    public async Task SendCriticalAlert(string title, string message, string details)
    {
        // Example: Send to Slack webhook
        var slackPayload = new
        {
            text = title,
            attachments = new[]
            {
                new
                {
                    color = "danger",
                    fields = new[]
                    {
                        new { title = "Message", value = message, @short = true },
                        new { title = "Time", value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"), @short = true },
                        new { title = "Details", value = $"```{details.Substring(0, Math.Min(1000, details.Length))}```", @short = false }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(slackPayload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Replace with your actual Slack webhook URL
        // await _httpClient.PostAsync("https://hooks.slack.com/services/YOUR/WEBHOOK/URL", content);
    }
}
```

### Application Insights Integration

```csharp
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Plugin.ExceptionListeners;
using Plugin.ExceptionListeners.Listeners;

public class ApplicationInsightsExceptionHandler
{
    private readonly TelemetryClient _telemetryClient;
    private readonly List<ExceptionListener> _listeners = new();

    public ApplicationInsightsExceptionHandler(string instrumentationKey)
    {
        var config = TelemetryConfiguration.CreateDefault();
        config.InstrumentationKey = instrumentationKey;
        _telemetryClient = new TelemetryClient(config);
    }

    public void Initialize()
    {
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleUnhandledException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException));

        _telemetryClient.TrackEvent("ExceptionHandlingInitialized");
    }

    private void HandleUnhandledException(object? sender, ExceptionEventArgs e)
    {
        var properties = new Dictionary<string, string>
        {
            ["Source"] = sender?.GetType().Name ?? "Unknown",
            ["ExceptionType"] = e.Exception.GetType().Name,
            ["MachineName"] = Environment.MachineName,
            ["UserName"] = Environment.UserName,
            ["OSVersion"] = Environment.OSVersion.ToString()
        };

        var metrics = new Dictionary<string, double>
        {
            ["ThreadId"] = Thread.CurrentThread.ManagedThreadId
        };

        // Track exception with rich telemetry
        _telemetryClient.TrackException(e.Exception, properties, metrics);

        // Track custom event for alerting
        _telemetryClient.TrackEvent("CriticalUnhandledException", properties, metrics);

        // Set user context if available
        _telemetryClient.Context.User.Id = GetCurrentUserId();
        _telemetryClient.Context.Session.Id = GetCurrentSessionId();
    }

    private void HandleTaskException(object? sender, ExceptionEventArgs e)
    {
        var properties = new Dictionary<string, string>
        {
            ["Source"] = "TaskScheduler",
            ["ExceptionType"] = e.Exception.GetType().Name
        };

        _telemetryClient.TrackException(e.Exception, properties);
        _telemetryClient.TrackEvent("UnobservedTaskException", properties);
    }

    private string GetCurrentUserId()
    {
        // Implement user identification logic
        return Environment.UserName;
    }

    private string GetCurrentSessionId()
    {
        // Implement session identification logic
        return Environment.TickCount.ToString();
    }

    public void Dispose()
    {
        _telemetryClient.TrackEvent("ExceptionHandlingDisposed");
        _telemetryClient.Flush();

        foreach (var listener in _listeners)
        {
            listener.Dispose();
        }
        _listeners.Clear();

        _telemetryClient.Dispose();
    }
}
```

## Microservices Architecture

### Distributed Exception Tracking

```csharp
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Plugin.ExceptionListeners;
using Plugin.ExceptionListeners.Listeners;

public class DistributedExceptionTracker
{
    private readonly ActivitySource _activitySource;
    private readonly ILogger<DistributedExceptionTracker> _logger;
    private readonly List<ExceptionListener> _listeners = new();

    public DistributedExceptionTracker(ILogger<DistributedExceptionTracker> logger)
    {
        _logger = logger;
        _activitySource = new ActivitySource("MyService.ExceptionHandling");
    }

    public void Initialize()
    {
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleUnhandledException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException));

        _logger.LogInformation("Distributed exception tracking initialized");
    }

    private void HandleUnhandledException(object? sender, ExceptionEventArgs e)
    {
        using var activity = _activitySource.StartActivity("HandleUnhandledException");

        // Add distributed tracing context
        activity?.SetTag("exception.type", e.Exception.GetType().Name);
        activity?.SetTag("exception.message", e.Exception.Message);
        activity?.SetTag("service.name", "MyService");
        activity?.SetTag("service.version", GetServiceVersion());

        // Get correlation context from current activity
        var traceId = Activity.Current?.TraceId.ToString();
        var spanId = Activity.Current?.SpanId.ToString();

        var correlationContext = new Dictionary<string, object?>
        {
            ["TraceId"] = traceId,
            ["SpanId"] = spanId,
            ["ServiceName"] = "MyService",
            ["ServiceVersion"] = GetServiceVersion(),
            ["Timestamp"] = DateTimeOffset.UtcNow,
            ["MachineName"] = Environment.MachineName
        };

        _logger.LogCritical(e.Exception,
            "Unhandled exception in distributed system. TraceId: {TraceId}, SpanId: {SpanId}",
            traceId, spanId);

        // Send to distributed error tracking
        _ = Task.Run(async () =>
        {
            try
            {
                await ReportToDistributedSystem(e.Exception, correlationContext);
            }
            catch (Exception reportingEx)
            {
                _logger.LogError(reportingEx, "Failed to report to distributed system");
            }
        });

        activity?.SetStatus(ActivityStatusCode.Error, e.Exception.Message);
    }

    private void HandleTaskException(object? sender, ExceptionEventArgs e)
    {
        using var activity = _activitySource.StartActivity("HandleTaskException");

        activity?.SetTag("exception.type", e.Exception.GetType().Name);
        activity?.SetTag("exception.source", "TaskScheduler");

        var traceId = Activity.Current?.TraceId.ToString();

        _logger.LogError(e.Exception,
            "Unobserved task exception in distributed system. TraceId: {TraceId}",
            traceId);
    }

    private async Task ReportToDistributedSystem(Exception exception, Dictionary<string, object?> context)
    {
        var errorReport = new DistributedErrorReport
        {
            Id = Guid.NewGuid(),
            Exception = SerializeException(exception),
            Context = context,
            Timestamp = DateTimeOffset.UtcNow,
            ServiceName = "MyService",
            ServiceVersion = GetServiceVersion()
        };

        // Send to message queue for processing by error tracking service
        await PublishErrorReport(errorReport);
    }

    private async Task PublishErrorReport(DistributedErrorReport report)
    {
        // Example: Send to RabbitMQ, Service Bus, or Kafka
        // var messagePublisher = new MessagePublisher();
        // await messagePublisher.PublishAsync("error-reports", report);

        // Placeholder for actual implementation
        await Task.CompletedTask;
    }

    private string SerializeException(Exception exception)
    {
        return JsonSerializer.Serialize(new
        {
            Type = exception.GetType().FullName,
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            InnerException = exception.InnerException?.ToString(),
            Data = exception.Data.Cast<DictionaryEntry>().ToDictionary(x => x.Key.ToString(), x => x.Value?.ToString())
        });
    }

    private string GetServiceVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
    }

    public void Dispose()
    {
        foreach (var listener in _listeners)
        {
            listener.Dispose();
        }
        _listeners.Clear();
        _activitySource.Dispose();
    }
}

public class DistributedErrorReport
{
    public Guid Id { get; set; }
    public string Exception { get; set; } = string.Empty;
    public Dictionary<string, object?> Context { get; set; } = new();
    public DateTimeOffset Timestamp { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string ServiceVersion { get; set; } = string.Empty;
}
```

## High-Performance Exception Aggregation

### Lock-Free Exception Statistics

```csharp
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Plugin.ExceptionListeners;
using Plugin.ExceptionListeners.Listeners;

public class HighPerformanceExceptionAggregator
{
    private readonly ExceptionStatistics _statistics = new();
    private readonly Timer _reportingTimer;
    private readonly List<ExceptionListener> _listeners = new();

    public HighPerformanceExceptionAggregator()
    {
        _listeners.Add(new CurrentDomainFirstChanceExceptionListener(HandleFirstChanceException));
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleUnhandledException));

        // Report statistics every 30 seconds
        _reportingTimer = new Timer(ReportStatistics, null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleFirstChanceException(object? sender, ExceptionEventArgs e)
    {
        // Ultra-fast path - just increment counters
        _statistics.RecordFirstChance(e.Exception.GetType());
    }

    private void HandleUnhandledException(object? sender, ExceptionEventArgs e)
    {
        _statistics.RecordUnhandled(e.Exception.GetType());

        // More detailed handling for critical exceptions
        _ = Task.Run(() => ProcessCriticalException(e.Exception));
    }

    private async Task ProcessCriticalException(Exception exception)
    {
        // Heavy processing on background thread
        await LogDetailedException(exception);
        await NotifyOpsTeam(exception);
    }

    private async Task LogDetailedException(Exception exception)
    {
        var logEntry = new
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = exception.GetType().FullName,
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            MachineName = Environment.MachineName,
            ProcessId = Environment.ProcessId
        };

        // Write to high-performance log
        await File.AppendAllTextAsync("critical-errors.log",
            JsonSerializer.Serialize(logEntry) + Environment.NewLine);
    }

    private async Task NotifyOpsTeam(Exception exception)
    {
        // Send immediate notification for critical errors
        // Implementation depends on your notification system
        await Task.CompletedTask;
    }

    private void ReportStatistics(object? state)
    {
        var stats = _statistics.GetAndReset();

        Console.WriteLine($"=== Exception Statistics (Last 30s) ===");
        Console.WriteLine($"Total First-Chance: {stats.TotalFirstChance}");
        Console.WriteLine($"Total Unhandled: {stats.TotalUnhandled}");
        Console.WriteLine($"Top Exception Types:");

        foreach (var kvp in stats.FirstChanceByType.OrderByDescending(x => x.Value).Take(10))
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }
    }

    public void Dispose()
    {
        _reportingTimer?.Dispose();
        foreach (var listener in _listeners)
        {
            listener.Dispose();
        }
        _listeners.Clear();
    }
}

// Lock-free statistics collector
public class ExceptionStatistics
{
    private long _totalFirstChance;
    private long _totalUnhandled;

    // Use concurrent collections for thread-safe access
    private readonly ConcurrentDictionary<string, long> _firstChanceByType = new();
    private readonly ConcurrentDictionary<string, long> _unhandledByType = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordFirstChance(Type exceptionType)
    {
        Interlocked.Increment(ref _totalFirstChance);

        var typeName = exceptionType.Name; // Faster than FullName
        _firstChanceByType.AddOrUpdate(typeName, 1, (k, v) => v + 1);
    }

    public void RecordUnhandled(Type exceptionType)
    {
        Interlocked.Increment(ref _totalUnhandled);

        var typeName = exceptionType.Name;
        _unhandledByType.AddOrUpdate(typeName, 1, (k, v) => v + 1);
    }

    public ExceptionStatsSnapshot GetAndReset()
    {
        var snapshot = new ExceptionStatsSnapshot
        {
            TotalFirstChance = Interlocked.Exchange(ref _totalFirstChance, 0),
            TotalUnhandled = Interlocked.Exchange(ref _totalUnhandled, 0),
            FirstChanceByType = GetAndClearDictionary(_firstChanceByType),
            UnhandledByType = GetAndClearDictionary(_unhandledByType)
        };

        return snapshot;
    }

    private Dictionary<string, long> GetAndClearDictionary(ConcurrentDictionary<string, long> source)
    {
        var snapshot = source.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        source.Clear();
        return snapshot;
    }
}

public class ExceptionStatsSnapshot
{
    public long TotalFirstChance { get; set; }
    public long TotalUnhandled { get; set; }
    public Dictionary<string, long> FirstChanceByType { get; set; } = new();
    public Dictionary<string, long> UnhandledByType { get; set; } = new();
}
```

## Circuit Breaker Pattern Integration

### Exception-Based Circuit Breaker

```csharp
using Plugin.ExceptionListeners;
using Plugin.ExceptionListeners.Listeners;

public class ExceptionBasedCircuitBreaker
{
    private readonly CircuitBreakerState _state = new();
    private readonly List<ExceptionListener> _listeners = new();
    private readonly ILogger<ExceptionBasedCircuitBreaker> _logger;

    public ExceptionBasedCircuitBreaker(ILogger<ExceptionBasedCircuitBreaker> logger)
    {
        _logger = logger;

        // Monitor unhandled exceptions for circuit breaker logic
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleUnhandledException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException));
    }

    private void HandleUnhandledException(object? sender, ExceptionEventArgs e)
    {
        _logger.LogCritical(e.Exception, "Unhandled exception - triggering circuit breaker evaluation");

        // Critical exceptions should open the circuit breaker immediately
        if (IsCriticalException(e.Exception))
        {
            _state.OpenCircuit("Critical unhandled exception");
        }
        else
        {
            _state.RecordFailure();
        }
    }

    private void HandleTaskException(object? sender, ExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Task exception - recording failure");

        // Task exceptions contribute to failure count
        _state.RecordFailure();
    }

    private bool IsCriticalException(Exception exception)
    {
        return exception is OutOfMemoryException ||
               exception is StackOverflowException ||
               exception is AccessViolationException ||
               (exception is SystemException && exception.Message.Contains("database"));
    }

    public bool IsCircuitOpen => _state.IsOpen;

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string operationName)
    {
        if (_state.IsOpen)
        {
            _logger.LogWarning("Circuit breaker is open - rejecting operation: {Operation}", operationName);
            throw new InvalidOperationException($"Circuit breaker is open for operation: {operationName}");
        }

        try
        {
            var result = await operation();
            _state.RecordSuccess();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation failed: {Operation}", operationName);
            _state.RecordFailure();
            throw;
        }
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

public class CircuitBreakerState
{
    private readonly object _lock = new();
    private int _failureCount = 0;
    private int _successCount = 0;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private bool _isOpen = false;
    private string _reason = string.Empty;

    private const int FailureThreshold = 5;
    private const int SuccessThreshold = 3;
    private static readonly TimeSpan OpenTimeout = TimeSpan.FromMinutes(1);

    public bool IsOpen
    {
        get
        {
            lock (_lock)
            {
                // Auto-close circuit breaker after timeout
                if (_isOpen && DateTime.UtcNow - _lastFailureTime > OpenTimeout)
                {
                    _isOpen = false;
                    _failureCount = 0;
                    _successCount = 0;
                    Console.WriteLine("Circuit breaker auto-closed after timeout");
                }

                return _isOpen;
            }
        }
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            _successCount++;

            // Close circuit breaker after enough successes
            if (_isOpen && _successCount >= SuccessThreshold)
            {
                _isOpen = false;
                _failureCount = 0;
                _successCount = 0;
                Console.WriteLine("Circuit breaker closed after successful operations");
            }
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;
            _successCount = 0; // Reset success count on failure

            // Open circuit breaker after threshold
            if (!_isOpen && _failureCount >= FailureThreshold)
            {
                OpenCircuit($"Failure threshold reached: {_failureCount} failures");
            }
        }
    }

    public void OpenCircuit(string reason)
    {
        lock (_lock)
        {
            _isOpen = true;
            _reason = reason;
            _lastFailureTime = DateTime.UtcNow;
            Console.WriteLine($"Circuit breaker opened: {reason}");
        }
    }
}
```

## Exception-Driven Health Monitoring

### Health Check Integration

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Plugin.ExceptionListeners;
using Plugin.ExceptionListeners.Listeners;

public class ExceptionHealthMonitor : IHealthCheck
{
    private static readonly ExceptionMetrics Metrics = new();
    private readonly List<ExceptionListener> _listeners = new();
    private readonly ILogger<ExceptionHealthMonitor> _logger;

    public ExceptionHealthMonitor(ILogger<ExceptionHealthMonitor> logger)
    {
        _logger = logger;

        // Set up listeners to monitor application health
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleUnhandledException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException));
    }

    private void HandleUnhandledException(object? sender, ExceptionEventArgs e)
    {
        Metrics.RecordUnhandledException(e.Exception);
        _logger.LogCritical(e.Exception, "Unhandled exception recorded for health monitoring");
    }

    private void HandleTaskException(object? sender, ExceptionEventArgs e)
    {
        Metrics.RecordTaskException(e.Exception);
        _logger.LogError(e.Exception, "Task exception recorded for health monitoring");
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var snapshot = Metrics.GetSnapshot();

        // Determine health based on recent exceptions
        var result = DetermineHealth(snapshot);

        return Task.FromResult(result);
    }

    private HealthCheckResult DetermineHealth(ExceptionMetricsSnapshot snapshot)
    {
        var data = new Dictionary<string, object>
        {
            ["UnhandledExceptions_Last5Minutes"] = snapshot.UnhandledExceptionsLast5Minutes,
            ["TaskExceptions_Last5Minutes"] = snapshot.TaskExceptionsLast5Minutes,
            ["TotalUnhandledExceptions"] = snapshot.TotalUnhandledExceptions,
            ["TotalTaskExceptions"] = snapshot.TotalTaskExceptions,
            ["LastUnhandledException"] = snapshot.LastUnhandledException?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "None",
            ["MostCommonException"] = snapshot.MostCommonExceptionType ?? "None"
        };

        // Critical: Any unhandled exceptions in last 5 minutes
        if (snapshot.UnhandledExceptionsLast5Minutes > 0)
        {
            return HealthCheckResult.Unhealthy(
                $"Detected {snapshot.UnhandledExceptionsLast5Minutes} unhandled exceptions in the last 5 minutes",
                data: data);
        }

        // Degraded: High number of task exceptions
        if (snapshot.TaskExceptionsLast5Minutes > 10)
        {
            return HealthCheckResult.Degraded(
                $"High number of task exceptions: {snapshot.TaskExceptionsLast5Minutes} in the last 5 minutes",
                data: data);
        }

        // Degraded: Many task exceptions recently
        if (snapshot.TaskExceptionsLast5Minutes > 5)
        {
            return HealthCheckResult.Degraded(
                $"Elevated task exceptions: {snapshot.TaskExceptionsLast5Minutes} in the last 5 minutes",
                data: data);
        }

        return HealthCheckResult.Healthy("No recent exceptions detected", data: data);
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

public class ExceptionMetrics
{
    private readonly object _lock = new();
    private readonly List<ExceptionRecord> _unhandledExceptions = new();
    private readonly List<ExceptionRecord> _taskExceptions = new();
    private readonly Dictionary<string, int> _exceptionTypeCounts = new();

    public void RecordUnhandledException(Exception exception)
    {
        lock (_lock)
        {
            _unhandledExceptions.Add(new ExceptionRecord(exception));
            UpdateTypeCounts(exception);
            CleanupOldRecords();
        }
    }

    public void RecordTaskException(Exception exception)
    {
        lock (_lock)
        {
            _taskExceptions.Add(new ExceptionRecord(exception));
            UpdateTypeCounts(exception);
            CleanupOldRecords();
        }
    }

    private void UpdateTypeCounts(Exception exception)
    {
        var typeName = exception.GetType().Name;
        _exceptionTypeCounts.TryGetValue(typeName, out var count);
        _exceptionTypeCounts[typeName] = count + 1;
    }

    private void CleanupOldRecords()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-30);
        _unhandledExceptions.RemoveAll(r => r.Timestamp < cutoff);
        _taskExceptions.RemoveAll(r => r.Timestamp < cutoff);
    }

    public ExceptionMetricsSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            CleanupOldRecords();

            var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);

            return new ExceptionMetricsSnapshot
            {
                UnhandledExceptionsLast5Minutes = _unhandledExceptions.Count(r => r.Timestamp >= fiveMinutesAgo),
                TaskExceptionsLast5Minutes = _taskExceptions.Count(r => r.Timestamp >= fiveMinutesAgo),
                TotalUnhandledExceptions = _unhandledExceptions.Count,
                TotalTaskExceptions = _taskExceptions.Count,
                LastUnhandledException = _unhandledExceptions.LastOrDefault()?.Timestamp,
                MostCommonExceptionType = _exceptionTypeCounts.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key
            };
        }
    }

    private class ExceptionRecord
    {
        public DateTime Timestamp { get; }
        public string TypeName { get; }
        public string Message { get; }

        public ExceptionRecord(Exception exception)
        {
            Timestamp = DateTime.UtcNow;
            TypeName = exception.GetType().Name;
            Message = exception.Message ?? string.Empty;
        }
    }
}

public class ExceptionMetricsSnapshot
{
    public int UnhandledExceptionsLast5Minutes { get; set; }
    public int TaskExceptionsLast5Minutes { get; set; }
    public int TotalUnhandledExceptions { get; set; }
    public int TotalTaskExceptions { get; set; }
    public DateTime? LastUnhandledException { get; set; }
    public string? MostCommonExceptionType { get; set; }
}

// Startup configuration
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register health check
        services.AddHealthChecks()
            .AddCheck<ExceptionHealthMonitor>("exceptions");

        // Register exception monitor as singleton
        services.AddSingleton<ExceptionHealthMonitor>();
    }

    public void Configure(IApplicationBuilder app)
    {
        // Initialize exception monitoring
        var exceptionMonitor = app.ApplicationServices.GetService<ExceptionHealthMonitor>();

        // Add health check endpoint
        app.UseHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var response = JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(x => new
                    {
                        name = x.Key,
                        status = x.Value.Status.ToString(),
                        description = x.Value.Description,
                        data = x.Value.Data
                    })
                });
                await context.Response.WriteAsync(response);
            }
        });
    }
}
```

These advanced scenarios demonstrate how Plugin.ExceptionListeners can be integrated into enterprise-grade applications with sophisticated monitoring, alerting, and resilience patterns.
