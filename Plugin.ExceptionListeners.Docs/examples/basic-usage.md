# Basic Usage Examples

This section provides practical examples of how to use Plugin.ExceptionListeners in common scenarios.

## Console Application Example

### Simple Exception Monitoring

```csharp
using Plugin.ExceptionListeners;
using Plugin.ExceptionListeners.Listeners;

namespace BasicConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting exception monitoring...");

            // Set up exception listeners
            using var unhandledListener = new CurrentDomainUnhandledExceptionListener(HandleUnhandledException);
            using var taskListener = new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException);

            Console.WriteLine("Exception listeners active. Running test scenarios...");

            // Test handled exceptions (won't trigger unhandled listener)
            await TestHandledExceptions();

            // Test unobserved task exceptions
            await TestUnobservedTaskExceptions();

            Console.WriteLine("Tests completed. Press any key to exit...");
            Console.ReadKey();
        }

        private static void HandleUnhandledException(object? sender, ExceptionEventArgs e)
        {
            Console.WriteLine($"[CRITICAL] Unhandled exception: {e.Exception.GetType().Name}");
            Console.WriteLine($"Message: {e.Exception.Message}");

            // In a real application, you would:
            // - Log to file/database
            // - Send to monitoring service
            // - Perform cleanup operations

            LogToFile("UNHANDLED", e.Exception);
        }

        private static void HandleTaskException(object? sender, ExceptionEventArgs e)
        {
            Console.WriteLine($"[WARNING] Unobserved task exception: {e.Exception.GetType().Name}");
            Console.WriteLine($"Message: {e.Exception.Message}");

            LogToFile("TASK", e.Exception);
        }

        private static async Task TestHandledExceptions()
        {
            Console.WriteLine("\n--- Testing Handled Exceptions ---");

            // These exceptions are caught and handled, so won't trigger unhandled listener
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    throw new InvalidOperationException($"Test exception {i + 1}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Caught and handled: {ex.Message}");
                }
            }
        }

        private static async Task TestUnobservedTaskExceptions()
        {
            Console.WriteLine("\n--- Testing Unobserved Task Exceptions ---");

            // Create tasks that fail but don't await them
            for (int i = 0; i < 3; i++)
            {
                var taskNumber = i + 1;
                Task.Run(async () =>
                {
                    await Task.Delay(50);
                    throw new InvalidDataException($"Unobserved task exception {taskNumber}");
                });
                // Not awaiting these tasks makes their exceptions "unobserved"
            }

            // Give tasks time to fail
            await Task.Delay(200);

            // Force garbage collection to trigger unobserved task exception events
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Give time for the events to be processed
            await Task.Delay(100);
        }

        private static void LogToFile(string type, Exception exception)
        {
            try
            {
                var logEntry = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} [{type}] {exception.GetType().Name}: {exception.Message}\n";
                File.AppendAllText("exceptions.log", logEntry);
            }
            catch
            {
                // Don't let logging failures crash the application
            }
        }
    }
}
```

## Web Application Example

### ASP.NET Core Integration

```csharp
// Program.cs
using Plugin.ExceptionListeners;
using Plugin.ExceptionListeners.Listeners;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddSingleton<IExceptionHandlingService, ExceptionHandlingService>();

var app = builder.Build();

// Initialize exception handling
var exceptionService = app.Services.GetRequiredService<IExceptionHandlingService>();
exceptionService.Initialize();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();
app.MapControllers();

app.Run();

// Exception handling service
public interface IExceptionHandlingService
{
    void Initialize();
}

public class ExceptionHandlingService : IExceptionHandlingService, IDisposable
{
    private readonly ILogger<ExceptionHandlingService> _logger;
    private readonly List<ExceptionListener> _listeners = new();

    public ExceptionHandlingService(ILogger<ExceptionHandlingService> logger)
    {
        _logger = logger;
    }

    public void Initialize()
    {
        _logger.LogInformation("Initializing exception handling...");

        // Set up listeners
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleUnhandledException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException));

        _logger.LogInformation("Exception handling initialized with {Count} listeners", _listeners.Count);
    }

    private void HandleUnhandledException(object? sender, ExceptionEventArgs e)
    {
        _logger.LogCritical(e.Exception, "Unhandled exception occurred in web application");

        // In production, you might want to:
        // - Send to Application Insights
        // - Report to Sentry
        // - Send alert notifications
        // - Save critical application state
    }

    private void HandleTaskException(object? sender, ExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unobserved task exception in web application");

        // Task exceptions are automatically marked as observed
        // This prevents them from crashing the application
    }

    public void Dispose()
    {
        _logger.LogInformation("Disposing exception handling service...");

        foreach (var listener in _listeners)
        {
            listener.Dispose();
        }
        _listeners.Clear();
    }
}

// Example controller
[ApiController]
[Route("[controller]")]
public class TestController : ControllerBase
{
    private readonly ILogger<TestController> _logger;

    public TestController(ILogger<TestController> logger)
    {
        _logger = logger;
    }

    [HttpGet("handled-exception")]
    public IActionResult TestHandledException()
    {
        try
        {
            throw new InvalidOperationException("This exception will be handled normally");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Handled exception in controller");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("unobserved-task")]
    public IActionResult TestUnobservedTask()
    {
        // Create a fire-and-forget task that will fail
        Task.Run(async () =>
        {
            await Task.Delay(100);
            throw new InvalidDataException("This will become an unobserved task exception");
        });

        return Ok(new { message = "Unobserved task started - check logs for exception" });
    }

    [HttpGet("background-work")]
    public IActionResult TestBackgroundWork()
    {
        // Proper way to handle background tasks
        _ = Task.Run(async () =>
        {
            try
            {
                await DoBackgroundWork();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background work failed");
            }
        });

        return Ok(new { message = "Background work started" });
    }

    private async Task DoBackgroundWork()
    {
        await Task.Delay(200);
        // Simulate work that might fail
        if (Random.Shared.Next(2) == 0)
        {
            throw new InvalidOperationException("Simulated background work failure");
        }
    }
}
```

## Windows Service Example

### Background Service with Exception Handling

```csharp
using Microsoft.Extensions.Hosting;
using Plugin.ExceptionListeners;
using Plugin.ExceptionListeners.Listeners;

// Program.cs
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "ExceptionListenerService";
});

builder.Services.AddHostedService<ExceptionMonitoringService>();
builder.Services.AddSingleton<IExceptionReportingService, ExceptionReportingService>();

var host = builder.Build();
host.Run();

// Main service
public class ExceptionMonitoringService : BackgroundService
{
    private readonly ILogger<ExceptionMonitoringService> _logger;
    private readonly IExceptionReportingService _reportingService;
    private readonly List<ExceptionListener> _listeners = new();

    public ExceptionMonitoringService(
        ILogger<ExceptionMonitoringService> logger,
        IExceptionReportingService reportingService)
    {
        _logger = logger;
        _reportingService = reportingService;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting exception monitoring service...");

        // Initialize exception listeners
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleUnhandledException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException));

        _logger.LogInformation("Exception listeners initialized");

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Exception monitoring service is running");

        // Main service loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Simulate service work
                await DoServiceWork(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in service main loop");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task DoServiceWork(CancellationToken cancellationToken)
    {
        // Simulate various service operations that might fail
        var tasks = new[]
        {
            ProcessDataAsync(cancellationToken),
            CheckSystemHealthAsync(cancellationToken),
            CleanupOldFilesAsync(cancellationToken)
        };

        // Process tasks and handle any failures
        var results = await Task.WhenAll(tasks.Select(async task =>
        {
            try
            {
                await task;
                return new { Success = true, Error = (Exception?)null };
            }
            catch (Exception ex)
            {
                return new { Success = false, Error = ex };
            }
        }));

        // Log any failures
        foreach (var result in results.Where(r => !r.Success))
        {
            _logger.LogError(result.Error, "Service task failed");
        }
    }

    private async Task ProcessDataAsync(CancellationToken cancellationToken)
    {
        // Simulate data processing
        await Task.Delay(100, cancellationToken);

        // Occasionally throw an exception to test handling
        if (Random.Shared.Next(20) == 0)
        {
            throw new InvalidDataException("Simulated data processing error");
        }
    }

    private async Task CheckSystemHealthAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken);
        // Health check logic
    }

    private async Task CleanupOldFilesAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(75, cancellationToken);
        // File cleanup logic
    }

    private void HandleUnhandledException(object? sender, ExceptionEventArgs e)
    {
        _logger.LogCritical(e.Exception, "CRITICAL: Unhandled exception in service");

        // Report critical errors
        _ = Task.Run(async () =>
        {
            try
            {
                await _reportingService.ReportCriticalErrorAsync(e.Exception);
            }
            catch (Exception reportingEx)
            {
                _logger.LogError(reportingEx, "Failed to report critical error");
            }
        });
    }

    private void HandleTaskException(object? sender, ExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unobserved task exception in service");

        // Report async errors
        _ = Task.Run(async () =>
        {
            try
            {
                await _reportingService.ReportAsyncErrorAsync(e.Exception);
            }
            catch
            {
                // Ignore reporting failures for task exceptions
            }
        });
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping exception monitoring service...");

        // Clean up listeners
        foreach (var listener in _listeners)
        {
            listener.Dispose();
        }
        _listeners.Clear();

        await base.StopAsync(cancellationToken);
        _logger.LogInformation("Exception monitoring service stopped");
    }
}

// Exception reporting service
public interface IExceptionReportingService
{
    Task ReportCriticalErrorAsync(Exception exception);
    Task ReportAsyncErrorAsync(Exception exception);
}

public class ExceptionReportingService : IExceptionReportingService
{
    private readonly ILogger<ExceptionReportingService> _logger;
    private readonly HttpClient _httpClient;

    public ExceptionReportingService(ILogger<ExceptionReportingService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
    }

    public async Task ReportCriticalErrorAsync(Exception exception)
    {
        try
        {
            // Example: Send to monitoring service
            var report = new
            {
                Type = "Critical",
                Exception = exception.ToString(),
                Timestamp = DateTimeOffset.UtcNow,
                MachineName = Environment.MachineName,
                ServiceName = "ExceptionListenerService"
            };

            // Send to your monitoring endpoint
            // await _httpClient.PostAsJsonAsync("https://your-monitoring-service.com/api/errors", report);

            _logger.LogInformation("Critical error reported successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to report critical error");
        }
    }

    public async Task ReportAsyncErrorAsync(Exception exception)
    {
        try
        {
            var report = new
            {
                Type = "AsyncError",
                Exception = exception.ToString(),
                Timestamp = DateTimeOffset.UtcNow,
                MachineName = Environment.MachineName
            };

            // Send to monitoring service
            // await _httpClient.PostAsJsonAsync("https://your-monitoring-service.com/api/errors", report);

            _logger.LogDebug("Async error reported successfully");
        }
        catch
        {
            // Silent failure for async error reporting
        }
    }
}
```

## Desktop Application Example (WPF)

### WPF Application with Exception Handling

```csharp
// App.xaml.cs
using System.Windows;
using Plugin.ExceptionListeners;
using Plugin.ExceptionListeners.Listeners;

namespace WpfExceptionExample
{
    public partial class App : Application
    {
        private readonly List<ExceptionListener> _listeners = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            // Set up exception handling before starting the UI
            SetupExceptionHandling();

            base.OnStartup(e);
        }

        private void SetupExceptionHandling()
        {
            // Handle unhandled exceptions
            _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleUnhandledException));
            _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException));

            // Also handle WPF-specific unhandled exceptions
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        private void HandleUnhandledException(object? sender, ExceptionEventArgs e)
        {
            LogException("Unhandled", e.Exception);

            // Show user-friendly error message
            ShowErrorDialog("A critical error occurred", e.Exception);
        }

        private void HandleTaskException(object? sender, ExceptionEventArgs e)
        {
            LogException("Task", e.Exception);

            // For task exceptions, just log - don't show UI
        }

        private void OnDispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogException("Dispatcher", e.Exception);

            // Show error dialog and mark as handled to prevent crash
            ShowErrorDialog("An error occurred in the user interface", e.Exception);
            e.Handled = true;
        }

        private void LogException(string type, Exception exception)
        {
            try
            {
                var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{type}] {exception}\n\n";
                File.AppendAllText("application.log", logEntry);
            }
            catch
            {
                // Ignore logging failures
            }
        }

        private void ShowErrorDialog(string title, Exception exception)
        {
            // Use dispatcher to ensure we're on UI thread
            Dispatcher.Invoke(() =>
            {
                var message = $"Error: {exception.Message}\n\nThe error has been logged. " +
                             "Please contact support if the problem persists.";

                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Clean up exception listeners
            foreach (var listener in _listeners)
            {
                listener.Dispose();
            }
            _listeners.Clear();

            base.OnExit(e);
        }
    }
}

// MainWindow.xaml.cs
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void TestHandledException_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            throw new InvalidOperationException("This exception will be handled");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Caught exception: {ex.Message}", "Handled Exception",
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void TestUnhandledException_Click(object sender, RoutedEventArgs e)
    {
        // This will be caught by the DispatcherUnhandledException handler
        throw new InvalidOperationException("This will be an unhandled exception on the UI thread");
    }

    private void TestTaskException_Click(object sender, RoutedEventArgs e)
    {
        // Create an unobserved task exception
        Task.Run(async () =>
        {
            await Task.Delay(100);
            throw new InvalidDataException("This will be an unobserved task exception");
        });

        MessageBox.Show("Unobserved task created - check logs", "Task Exception Test",
                       MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void TestAsyncWork_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await DoAsyncWork();
            MessageBox.Show("Async work completed successfully", "Success",
                           MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Async work failed: {ex.Message}", "Error",
                           MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task DoAsyncWork()
    {
        await Task.Delay(1000); // Simulate work

        // Randomly fail to demonstrate exception handling
        if (Random.Shared.Next(2) == 0)
        {
            throw new InvalidOperationException("Simulated async work failure");
        }
    }
}
```

## Configuration Examples

### JSON Configuration

```json
// appsettings.json
{
  "ExceptionHandling": {
    "Enabled": true,
    "EnableFirstChanceListener": false,
    "EnableUnhandledListener": true,
    "EnableTaskListener": true,
    "EnableNativeListener": true,
    "LogLevel": "Information",
    "ReportingEndpoint": "https://your-monitoring-service.com/api/errors",
    "MaxReportsPerMinute": 60,
    "IgnoredExceptionTypes": [
      "System.OperationCanceledException",
      "System.Threading.Tasks.TaskCanceledException"
    ]
  }
}
```

### Configuration Service

```csharp
public class ExceptionHandlingConfiguration
{
    public bool Enabled { get; set; } = true;
    public bool EnableFirstChanceListener { get; set; } = false;
    public bool EnableUnhandledListener { get; set; } = true;
    public bool EnableTaskListener { get; set; } = true;
    public bool EnableNativeListener { get; set; } = true;
    public string LogLevel { get; set; } = "Information";
    public string? ReportingEndpoint { get; set; }
    public int MaxReportsPerMinute { get; set; } = 60;
    public List<string> IgnoredExceptionTypes { get; set; } = new();
}

public class ConfigurableExceptionService
{
    private readonly ExceptionHandlingConfiguration _config;
    private readonly ILogger<ConfigurableExceptionService> _logger;
    private readonly List<ExceptionListener> _listeners = new();

    public ConfigurableExceptionService(
        IOptions<ExceptionHandlingConfiguration> config,
        ILogger<ConfigurableExceptionService> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public void Initialize()
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("Exception handling is disabled by configuration");
            return;
        }

        if (_config.EnableUnhandledListener)
        {
            _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleUnhandledException));
            _logger.LogInformation("Unhandled exception listener enabled");
        }

        if (_config.EnableTaskListener)
        {
            _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException));
            _logger.LogInformation("Task exception listener enabled");
        }

        if (_config.EnableFirstChanceListener)
        {
            _listeners.Add(new CurrentDomainFirstChanceExceptionListener(HandleFirstChanceException));
            _logger.LogWarning("First-chance exception listener enabled - monitor performance");
        }

        _logger.LogInformation("Exception handling initialized with {Count} listeners", _listeners.Count);
    }

    private void HandleUnhandledException(object? sender, ExceptionEventArgs e)
    {
        if (ShouldIgnoreException(e.Exception))
            return;

        _logger.LogCritical(e.Exception, "Unhandled exception occurred");
    }

    private void HandleTaskException(object? sender, ExceptionEventArgs e)
    {
        if (ShouldIgnoreException(e.Exception))
            return;

        _logger.LogError(e.Exception, "Unobserved task exception occurred");
    }

    private void HandleFirstChanceException(object? sender, ExceptionEventArgs e)
    {
        if (ShouldIgnoreException(e.Exception))
            return;

        // Minimal processing for first-chance exceptions
        _logger.LogDebug("First-chance exception: {ExceptionType}", e.Exception.GetType().Name);
    }

    private bool ShouldIgnoreException(Exception exception)
    {
        return _config.IgnoredExceptionTypes.Contains(exception.GetType().FullName);
    }
}
```

These examples demonstrate various ways to integrate Plugin.ExceptionListeners into different types of applications, from simple console apps to complex web services and desktop applications.
