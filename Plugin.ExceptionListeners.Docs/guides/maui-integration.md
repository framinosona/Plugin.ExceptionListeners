# MAUI Integration Guide

This guide covers how to integrate Plugin.ExceptionListeners into .NET MAUI applications for comprehensive cross-platform exception handling.

## Overview

MAUI applications require special consideration for exception handling due to:

- **Multiple platforms** - iOS, Android, Windows, macOS each with different native exception sources
- **Native interop** - Platform-specific APIs can throw native exceptions
- **Lifecycle management** - Apps can be suspended, resumed, or terminated by the OS
- **UI thread considerations** - Exceptions on different threads need proper handling

## Installation

Install both packages in your MAUI project:

```bash
dotnet add package Plugin.ExceptionListeners
dotnet add package Plugin.ExceptionListeners.Maui
```

The MAUI package automatically includes the core package as a dependency.

## Basic Setup

### App.xaml.cs Integration

```csharp
using Plugin.ExceptionListeners.Listeners;
using Plugin.ExceptionListeners.Maui;

public partial class App : Application
{
    private ExceptionManager? _exceptionManager;

    public App()
    {
        InitializeComponent();

        // Initialize exception handling before setting MainPage
        InitializeExceptionHandling();

        MainPage = new AppShell();
    }

    private void InitializeExceptionHandling()
    {
        _exceptionManager = new ExceptionManager();
        _exceptionManager.Initialize();
    }

    protected override void OnStart()
    {
        _exceptionManager?.OnStart();
        base.OnStart();
    }

    protected override void OnSleep()
    {
        _exceptionManager?.OnSleep();
        base.OnSleep();
    }

    protected override void OnResume()
    {
        _exceptionManager?.OnResume();
        base.OnResume();
    }

    // Note: OnTerminate is not called on all platforms
    protected override void OnTerminate()
    {
        _exceptionManager?.OnTerminate();
        _exceptionManager?.Dispose();
        base.OnTerminate();
    }
}
```

### Exception Manager Implementation

```csharp
public class ExceptionManager : IDisposable
{
    private readonly List<ExceptionListener> _listeners = new();
    private readonly ILogger _logger;
    private bool _isInitialized = false;

    public ExceptionManager()
    {
        // Set up logging - adapt to your logging framework
        _logger = LoggerFactory.Create(builder => builder.AddDebug()).CreateLogger<ExceptionManager>();
    }

    public void Initialize()
    {
        if (_isInitialized) return;

        try
        {
            // Core .NET exception handling
            _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleUnhandledException));
            _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException));

            // Optional: FirstChanceExceptionListener (use carefully in production)
            #if DEBUG
            _listeners.Add(new CurrentDomainFirstChanceExceptionListener(HandleFirstChanceException));
            #endif

            // MAUI-specific native exception handling
            _listeners.Add(new NativeUnhandledExceptionListener(HandleNativeException));

            _logger.LogInformation("Exception handling initialized with {Count} listeners", _listeners.Count);
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            // Log initialization failure
            System.Diagnostics.Debug.WriteLine($"Failed to initialize exception handling: {ex}");
        }
    }

    public void OnStart()
    {
        _logger.LogInformation("App started - exception handling active");
    }

    public void OnSleep()
    {
        _logger.LogInformation("App sleeping - exception handling remains active");
        // Don't dispose listeners on sleep - app may resume
    }

    public void OnResume()
    {
        _logger.LogInformation("App resumed - exception handling active");
    }

    public void OnTerminate()
    {
        _logger.LogInformation("App terminating - disposing exception handling");
        Dispose();
    }

    private void HandleUnhandledException(object? sender, ExceptionEventArgs e)
    {
        _logger.LogCritical(e.Exception, "Unhandled exception occurred");

        // Attempt to save critical data
        _ = Task.Run(async () =>
        {
            try
            {
                await SaveCriticalDataAsync();
                await ReportCrashAsync(e.Exception);
            }
            catch
            {
                // Don't let error reporting crash the app further
            }
        });
    }

    private void HandleTaskException(object? sender, ExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unobserved task exception");
        // Task exceptions are automatically observed by the listener
    }

    #if DEBUG
    private void HandleFirstChanceException(object? sender, ExceptionEventArgs e)
    {
        // Only in debug builds - can be very noisy
        System.Diagnostics.Debug.WriteLine($"First chance: {e.Exception.GetType().Name}: {e.Exception.Message}");
    }
    #endif

    private void HandleNativeException(object? sender, ExceptionEventArgs e)
    {
        var platform = GetCurrentPlatform();
        _logger.LogError(e.Exception, "Native exception on {Platform}", platform);

        // Handle platform-specific exceptions
        HandlePlatformSpecificException(e.Exception, platform);
    }

    private void HandlePlatformSpecificException(Exception exception, string platform)
    {
        switch (platform.ToLower())
        {
            case "android":
                HandleAndroidException(exception);
                break;
            case "ios":
                HandleiOSException(exception);
                break;
            case "windows":
                HandleWindowsException(exception);
                break;
            case "maccatalyst":
                HandleMacCatalystException(exception);
                break;
        }
    }

    private void HandleAndroidException(Exception exception)
    {
        // Android-specific handling
        if (exception.Message?.Contains("Permission") == true)
        {
            _logger.LogWarning("Android permission-related exception: {Message}", exception.Message);
        }
    }

    private void HandleiOSException(Exception exception)
    {
        // iOS-specific handling
        if (exception.Message?.Contains("NSError") == true)
        {
            _logger.LogWarning("iOS NSError: {Message}", exception.Message);
        }
    }

    private void HandleWindowsException(Exception exception)
    {
        // Windows-specific handling
        if (exception.Message?.Contains("HRESULT") == true)
        {
            _logger.LogWarning("Windows HRESULT error: {Message}", exception.Message);
        }
    }

    private void HandleMacCatalystException(Exception exception)
    {
        // Mac Catalyst-specific handling
        _logger.LogWarning("Mac Catalyst exception: {Message}", exception.Message);
    }

    private string GetCurrentPlatform()
    {
#if ANDROID
        return "Android";
#elif IOS
        return "iOS";
#elif WINDOWS
        return "Windows";
#elif MACCATALYST
        return "MacCatalyst";
#else
        return "Unknown";
#endif
    }

    private async Task SaveCriticalDataAsync()
    {
        try
        {
            // Implement critical data saving logic
            // Example: Save user preferences, unsaved work, etc.
            await Task.Delay(100); // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save critical data during crash");
        }
    }

    private async Task ReportCrashAsync(Exception exception)
    {
        try
        {
            // Implement crash reporting
            // Example: Send to App Center, Sentry, custom service, etc.
            await Task.Delay(100); // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to report crash");
        }
    }

    public void Dispose()
    {
        foreach (var listener in _listeners)
        {
            try
            {
                listener.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing exception listener");
            }
        }

        _listeners.Clear();
        _isInitialized = false;
    }
}
```

## Platform-Specific Configuration

### Android Configuration

For Android apps, you may want to handle specific Android exceptions:

```csharp
// In Platforms/Android/MainApplication.cs or similar
#if ANDROID
using Android.Runtime;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override void OnCreate()
    {
        base.OnCreate();

        // Android-specific exception handling setup
        AndroidEnvironment.UnhandledExceptionRaiser += OnAndroidUnhandledException;
    }

    private void OnAndroidUnhandledException(object? sender, RaiseThrowableEventArgs e)
    {
        // This is handled by NativeUnhandledExceptionListener, but you can add
        // additional Android-specific logic here if needed
        System.Diagnostics.Debug.WriteLine($"Android unhandled exception: {e.Exception}");

        // Don't set e.Handled = true here - let the NativeUnhandledExceptionListener handle it
    }
}
#endif
```

### iOS Configuration

For iOS apps, configure exception handling in the platform-specific code:

```csharp
// In Platforms/iOS/AppDelegate.cs
#if IOS
using Foundation;
using ObjCRuntime;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication app, NSDictionary options)
    {
        // iOS-specific exception handling setup
        Runtime.MarshalObjectiveCException += OnMarshalObjectiveCException;
        Runtime.MarshalManagedException += OnMarshalManagedException;

        return base.FinishedLaunching(app, options);
    }

    private void OnMarshalObjectiveCException(object? sender, MarshalObjectiveCExceptionEventArgs e)
    {
        // This is handled by NativeUnhandledExceptionListener
        System.Diagnostics.Debug.WriteLine($"Objective-C exception: {e.Exception}");
    }

    private void OnMarshalManagedException(object? sender, MarshalManagedExceptionEventArgs e)
    {
        // This is handled by NativeUnhandledExceptionListener
        System.Diagnostics.Debug.WriteLine($"Managed exception in native context: {e.Exception}");
    }
}
#endif
```

## Integration with Popular Services

### App Center Crashes

```csharp
// Install: Microsoft.AppCenter.Crashes
using Microsoft.AppCenter.Crashes;

private void HandleUnhandledException(object? sender, ExceptionEventArgs e)
{
    // Report to App Center
    var properties = new Dictionary<string, string>
    {
        { "Platform", DeviceInfo.Platform.ToString() },
        { "Version", AppInfo.VersionString },
        { "Source", sender?.GetType().Name ?? "Unknown" }
    };

    Crashes.TrackError(e.Exception, properties);
}
```

### Sentry

```csharp
// Install: Sentry.Maui
using Sentry;

private void HandleUnhandledException(object? sender, ExceptionEventArgs e)
{
    SentrySdk.CaptureException(e.Exception, scope =>
    {
        scope.SetTag("platform", DeviceInfo.Platform.ToString());
        scope.SetTag("version", AppInfo.VersionString);
        scope.SetExtra("source", sender?.GetType().Name);
    });
}
```

### Custom Logging Service

```csharp
public class CustomCrashReporter
{
    private readonly HttpClient _httpClient;
    private readonly string _apiEndpoint;

    public CustomCrashReporter(string apiEndpoint)
    {
        _apiEndpoint = apiEndpoint;
        _httpClient = new HttpClient();
    }

    public async Task ReportCrashAsync(Exception exception)
    {
        try
        {
            var report = new
            {
                Exception = exception.ToString(),
                Platform = DeviceInfo.Platform.ToString(),
                Version = AppInfo.VersionString,
                DeviceModel = DeviceInfo.Model,
                OSVersion = DeviceInfo.VersionString,
                Timestamp = DateTimeOffset.UtcNow
            };

            var json = JsonSerializer.Serialize(report);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            await _httpClient.PostAsync(_apiEndpoint, content);
        }
        catch
        {
            // Don't let crash reporting crash the app
        }
    }
}
```

## Best Practices for MAUI

### 1. Lifecycle Management

```csharp
public partial class App : Application
{
    private ExceptionManager? _exceptionManager;

    protected override void CleanUp()
    {
        // Override CleanUp for proper disposal
        _exceptionManager?.Dispose();
        base.CleanUp();
    }
}
```

### 2. Thread Safety

```csharp
private void HandleException(object? sender, ExceptionEventArgs e)
{
    // Ensure UI updates happen on main thread
    if (Application.Current?.Dispatcher?.IsDispatchRequired == true)
    {
        Application.Current.Dispatcher.Dispatch(() => HandleExceptionOnMainThread(e));
    }
    else
    {
        HandleExceptionOnMainThread(e);
    }
}

private void HandleExceptionOnMainThread(ExceptionEventArgs e)
{
    // Safe to update UI here
    ShowErrorDialog(e.Exception.Message);
}
```

### 3. Performance Considerations

```csharp
// Use background processing for heavy operations
private void HandleException(object? sender, ExceptionEventArgs e)
{
    // Quick logging
    _logger.LogError(e.Exception, "Exception occurred");

    // Heavy operations on background thread
    _ = Task.Run(async () =>
    {
        try
        {
            await ProcessExceptionAsync(e.Exception);
        }
        catch
        {
            // Handle processing failures gracefully
        }
    });
}
```

### 4. Conditional Compilation

```csharp
private void HandleNativeException(object? sender, ExceptionEventArgs e)
{
#if DEBUG
    // Detailed logging in debug mode
    System.Diagnostics.Debug.WriteLine($"Full exception: {e.Exception}");
#else
    // Minimal logging in release mode
    _logger.LogError(e.Exception, "Native exception occurred");
#endif

    // Platform-specific handling
#if ANDROID
    HandleAndroidSpecificException(e.Exception);
#elif IOS
    HandleiOSSpecificException(e.Exception);
#endif
}
```

## Testing Exception Handling

### Unit Testing

```csharp
[Fact]
public void ExceptionManager_ShouldHandleUnhandledException()
{
    // Arrange
    var handled = false;
    var exceptionManager = new ExceptionManager();

    // Act
    exceptionManager.Initialize();

    // Simulate unhandled exception
    Task.Run(() => throw new InvalidOperationException("Test exception"));

    // Allow time for async processing
    Thread.Sleep(1000);
    GC.Collect();
    GC.WaitForPendingFinalizers();

    // Assert would depend on your implementation
    Assert.True(true); // Placeholder
}
```

### Integration Testing

```csharp
public class ExceptionHandlingTests
{
    [Fact]
    public async Task NativeExceptionListener_ShouldCatchPlatformExceptions()
    {
        // This would be platform-specific test code
        // Testing native exceptions requires platform-specific setup

        var caughtException = false;
        var listener = new NativeUnhandledExceptionListener((sender, e) =>
        {
            caughtException = true;
        });

        // Trigger platform-specific exception
        // Implementation depends on platform

        await Task.Delay(100); // Allow processing time

        Assert.True(caughtException);
    }
}
```

## Common Issues and Solutions

### Issue: Listeners Not Working on Specific Platforms

**Solution:** Ensure platform-specific configuration is correct:

```csharp
// Check platform availability
private void InitializePlatformSpecificHandling()
{
#if ANDROID
    if (Platform.CurrentActivity != null)
    {
        // Android-specific initialization
    }
#elif IOS
    if (UIApplication.SharedApplication != null)
    {
        // iOS-specific initialization
    }
#endif
}
```

### Issue: Excessive Exception Logging

**Solution:** Implement filtering:

```csharp
private readonly HashSet<string> _ignoredExceptions = new()
{
    "System.OperationCanceledException",
    "System.Threading.Tasks.TaskCanceledException"
};

private void HandleException(object? sender, ExceptionEventArgs e)
{
    if (_ignoredExceptions.Contains(e.Exception.GetType().FullName))
        return;

    // Process only relevant exceptions
    ProcessException(e.Exception);
}
```

### Issue: Memory Leaks from Exception Handlers

**Solution:** Proper cleanup:

```csharp
public class ExceptionManager : IDisposable
{
    private bool _disposed = false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Clean up managed resources
                foreach (var listener in _listeners)
                {
                    listener.Dispose();
                }
                _listeners.Clear();
            }

            _disposed = true;
        }
    }
}
```

This guide should help you implement robust exception handling in your MAUI applications across all supported platforms.
