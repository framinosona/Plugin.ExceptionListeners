# MAUI Application Examples

This section provides comprehensive examples for integrating Plugin.ExceptionListeners into .NET MAUI applications across different platforms.

## Basic MAUI Setup

### Complete App.xaml.cs Implementation

```csharp
using Plugin.ExceptionListeners.Listeners;
using Plugin.ExceptionListeners.Maui;

namespace MauiExceptionExample;

public partial class App : Application
{
    private ExceptionManager? _exceptionManager;

    public App()
    {
        InitializeComponent();

        // Initialize exception handling before creating main page
        InitializeExceptionHandling();

        MainPage = new AppShell();
    }

    private void InitializeExceptionHandling()
    {
        try
        {
            _exceptionManager = new ExceptionManager();
            _exceptionManager.Initialize();

            System.Diagnostics.Debug.WriteLine("Exception handling initialized successfully");
        }
        catch (Exception ex)
        {
            // Fallback logging if exception handling setup fails
            System.Diagnostics.Debug.WriteLine($"Failed to initialize exception handling: {ex}");
        }
    }

    protected override void OnStart()
    {
        _exceptionManager?.OnAppStart();
        base.OnStart();
    }

    protected override void OnSleep()
    {
        _exceptionManager?.OnAppSleep();
        base.OnSleep();
    }

    protected override void OnResume()
    {
        _exceptionManager?.OnAppResume();
        base.OnResume();
    }

    protected override void CleanUp()
    {
        _exceptionManager?.Dispose();
        base.CleanUp();
    }
}

public class ExceptionManager : IDisposable
{
    private readonly List<ExceptionListener> _listeners = new();
    private readonly MauiExceptionLogger _logger = new();
    private bool _isDisposed = false;

    public void Initialize()
    {
        // Core .NET exception handling
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleUnhandledException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException));

        // MAUI native exception handling
        _listeners.Add(new NativeUnhandledExceptionListener(HandleNativeException));

        _logger.LogInfo($"Initialized {_listeners.Count} exception listeners");
    }

    public void OnAppStart()
    {
        _logger.LogInfo("App started - exception monitoring active");
    }

    public void OnAppSleep()
    {
        _logger.LogInfo("App sleeping - exception monitoring remains active");
    }

    public void OnAppResume()
    {
        _logger.LogInfo("App resumed - exception monitoring active");
    }

    private void HandleUnhandledException(object? sender, ExceptionEventArgs e)
    {
        _logger.LogCritical(e.Exception, "Unhandled exception occurred", GetExceptionContext(sender));

        // Attempt to save critical data asynchronously
        _ = Task.Run(async () =>
        {
            try
            {
                await SaveCriticalApplicationState();
                await ReportCrashToService(e.Exception);
            }
            catch (Exception reportEx)
            {
                _logger.LogError(reportEx, "Failed to report crash");
            }
        });

        // Show user notification on UI thread
        ShowErrorToUser(e.Exception);
    }

    private void HandleTaskException(object? sender, ExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unobserved task exception", GetExceptionContext(sender));
    }

    private void HandleNativeException(object? sender, ExceptionEventArgs e)
    {
        var platform = GetCurrentPlatform();
        _logger.LogError(e.Exception, $"Native exception on {platform}", GetExceptionContext(sender));

        // Handle platform-specific recovery
        HandlePlatformSpecificRecovery(e.Exception, platform);
    }

    private ExceptionContext GetExceptionContext(object? sender)
    {
        return new ExceptionContext
        {
            Platform = GetCurrentPlatform(),
            AppVersion = GetAppVersion(),
            DeviceInfo = GetDeviceInfo(),
            Source = sender?.GetType().Name ?? "Unknown",
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private void ShowErrorToUser(Exception exception)
    {
        // Ensure we're on the UI thread
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var userMessage = GetUserFriendlyMessage(exception);
                await Application.Current?.MainPage?.DisplayAlert(
                    "Unexpected Error",
                    userMessage + "\n\nThe error has been logged and reported.",
                    "OK");
            }
            catch
            {
                // If even showing the error fails, just debug log it
                System.Diagnostics.Debug.WriteLine($"Failed to show error dialog: {exception}");
            }
        });
    }

    private string GetUserFriendlyMessage(Exception exception)
    {
        return exception switch
        {
            HttpRequestException => "Network connection error. Please check your internet connection.",
            TimeoutException => "The operation took too long to complete. Please try again.",
            UnauthorizedAccessException => "Access denied. Please check your permissions.",
            _ => "An unexpected error occurred."
        };
    }

    private void HandlePlatformSpecificRecovery(Exception exception, string platform)
    {
        switch (platform.ToLower())
        {
            case "android":
                HandleAndroidRecovery(exception);
                break;
            case "ios":
                HandleiOSRecovery(exception);
                break;
            case "windows":
                HandleWindowsRecovery(exception);
                break;
        }
    }

    private void HandleAndroidRecovery(Exception exception)
    {
        if (exception.Message?.Contains("Permission") == true)
        {
            _logger.LogWarning("Android permission issue detected");
            // Could trigger permission request flow
        }
    }

    private void HandleiOSRecovery(Exception exception)
    {
        if (exception.Message?.Contains("NSError") == true)
        {
            _logger.LogWarning("iOS system error detected");
            // iOS-specific recovery logic
        }
    }

    private void HandleWindowsRecovery(Exception exception)
    {
        if (exception.Message?.Contains("HRESULT") == true)
        {
            _logger.LogWarning("Windows system error detected");
            // Windows-specific recovery logic
        }
    }

    private async Task SaveCriticalApplicationState()
    {
        try
        {
            // Save any unsaved user data
            var preferences = Preferences.Default;
            preferences.Set("LastCrashTime", DateTimeOffset.UtcNow.ToString());
            preferences.Set("CrashRecoveryNeeded", true);

            // Save any in-memory data to local storage
            // Implementation depends on your app's data model
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save critical application state");
        }
    }

    private async Task ReportCrashToService(Exception exception)
    {
        try
        {
            var crashReport = new CrashReport
            {
                Exception = exception.ToString(),
                Platform = GetCurrentPlatform(),
                AppVersion = GetAppVersion(),
                DeviceModel = DeviceInfo.Model,
                OSVersion = DeviceInfo.VersionString,
                Timestamp = DateTimeOffset.UtcNow,
                UserId = GetUserId()
            };

            // Send to crash reporting service (App Center, Sentry, etc.)
            await SendCrashReport(crashReport);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to report crash to service");
        }
    }

    private async Task SendCrashReport(CrashReport report)
    {
        // Example implementation - replace with your crash reporting service
        using var httpClient = new HttpClient();
        var json = System.Text.Json.JsonSerializer.Serialize(report);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // await httpClient.PostAsync("https://your-crash-service.com/api/crashes", content);
        await Task.CompletedTask; // Placeholder
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

    private string GetAppVersion()
    {
        return AppInfo.VersionString;
    }

    private DeviceInformation GetDeviceInfo()
    {
        return new DeviceInformation
        {
            Model = DeviceInfo.Model,
            Manufacturer = DeviceInfo.Manufacturer,
            Platform = DeviceInfo.Platform.ToString(),
            OSVersion = DeviceInfo.VersionString,
            DeviceType = DeviceInfo.DeviceType.ToString()
        };
    }

    private string GetUserId()
    {
        // Return anonymous ID or actual user ID based on your app's authentication
        return Preferences.Get("AnonymousUserId", Guid.NewGuid().ToString());
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        foreach (var listener in _listeners)
        {
            try
            {
                listener.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing listener");
            }
        }
        _listeners.Clear();
        _isDisposed = true;
    }
}

public class ExceptionContext
{
    public string Platform { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public DeviceInformation? DeviceInfo { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}

public class DeviceInformation
{
    public string Model { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
}

public class CrashReport
{
    public string Exception { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class MauiExceptionLogger
{
    public void LogInfo(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} - {message}");
    }

    public void LogWarning(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[WARN] {DateTime.Now:HH:mm:ss} - {message}");
    }

    public void LogError(Exception exception, string message, ExceptionContext? context = null)
    {
        var contextInfo = context != null ? $" | Platform: {context.Platform} | Source: {context.Source}" : "";
        System.Diagnostics.Debug.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss} - {message}{contextInfo}");
        System.Diagnostics.Debug.WriteLine($"Exception: {exception}");
    }

    public void LogCritical(Exception exception, string message, ExceptionContext? context = null)
    {
        var contextInfo = context != null ? $" | Platform: {context.Platform} | Source: {context.Source}" : "";
        System.Diagnostics.Debug.WriteLine($"[CRITICAL] {DateTime.Now:HH:mm:ss} - {message}{contextInfo}");
        System.Diagnostics.Debug.WriteLine($"Exception: {exception}");
    }
}
```

## Platform-Specific Implementations

### Android-Specific Exception Handling

```csharp
// Platforms/Android/MainApplication.cs
#if ANDROID
using Android.App;
using Android.Runtime;
using AndroidX.Lifecycle;

[Application]
public class MainApplication : MauiApplication, ILifecycleObserver
{
    private AndroidExceptionHandler? _androidExceptionHandler;

    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override void OnCreate()
    {
        base.OnCreate();

        // Set up Android-specific exception handling
        _androidExceptionHandler = new AndroidExceptionHandler();
        _androidExceptionHandler.Initialize();

        // Register for lifecycle events
        ProcessLifecycleOwner.Get().Lifecycle.AddObserver(this);
    }

    [Lifecycle.Event.OnStart]
    public void OnAppStart()
    {
        _androidExceptionHandler?.OnAppStart();
    }

    [Lifecycle.Event.OnStop]
    public void OnAppStop()
    {
        _androidExceptionHandler?.OnAppStop();
    }

    public override void OnTerminate()
    {
        _androidExceptionHandler?.Dispose();
        base.OnTerminate();
    }
}

public class AndroidExceptionHandler : IDisposable
{
    private bool _isInitialized = false;

    public void Initialize()
    {
        if (_isInitialized) return;

        // Handle Android-specific unhandled exceptions
        AndroidEnvironment.UnhandledExceptionRaiser += OnAndroidUnhandledException;

        // Handle Java exceptions that cross into managed code
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        _isInitialized = true;
        System.Diagnostics.Debug.WriteLine("Android exception handler initialized");
    }

    private void OnAndroidUnhandledException(object? sender, RaiseThrowableEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"Android unhandled exception: {e.Exception}");

            // Log to Android log
            Android.Util.Log.Error("MauiApp", $"Unhandled exception: {e.Exception}");

            // Handle specific Android exception types
            HandleAndroidSpecificException(e.Exception);

            // Mark as handled to prevent crash
            e.Handled = true;
        }
        catch (Exception handlerEx)
        {
            System.Diagnostics.Debug.WriteLine($"Error in Android exception handler: {handlerEx}");
        }
    }

    private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Domain unhandled exception on Android: {exception}");
            Android.Util.Log.Fatal("MauiApp", $"Fatal exception: {exception}");
        }
    }

    private void HandleAndroidSpecificException(Java.Lang.Throwable throwable)
    {
        switch (throwable)
        {
            case Java.Lang.OutOfMemoryError:
                HandleAndroidOutOfMemory();
                break;
            case Java.Lang.SecurityException secEx:
                HandleAndroidSecurityException(secEx);
                break;
            case Java.Net.SocketTimeoutException timeoutEx:
                HandleAndroidNetworkTimeout(timeoutEx);
                break;
            default:
                HandleGenericAndroidException(throwable);
                break;
        }
    }

    private void HandleAndroidOutOfMemory()
    {
        System.Diagnostics.Debug.WriteLine("Android out of memory - attempting recovery");

        // Trigger aggressive garbage collection
        Java.Lang.JavaSystem.Gc();
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();

        // Clear image caches if you're using image loading libraries
        // ClearImageCaches();
    }

    private void HandleAndroidSecurityException(Java.Lang.SecurityException secEx)
    {
        System.Diagnostics.Debug.WriteLine($"Android security exception: {secEx.Message}");

        // Could trigger permission request flow
        if (secEx.Message?.Contains("permission") == true)
        {
            // Handle permission-related errors
            RequestRequiredPermissions();
        }
    }

    private void HandleAndroidNetworkTimeout(Java.Net.SocketTimeoutException timeoutEx)
    {
        System.Diagnostics.Debug.WriteLine($"Android network timeout: {timeoutEx.Message}");

        // Could implement network retry logic
        // NetworkRetryManager.ScheduleRetry();
    }

    private void HandleGenericAndroidException(Java.Lang.Throwable throwable)
    {
        System.Diagnostics.Debug.WriteLine($"Generic Android exception: {throwable.GetType().Name} - {throwable.Message}");
    }

    private void RequestRequiredPermissions()
    {
        // Implementation depends on your app's permission requirements
        System.Diagnostics.Debug.WriteLine("Should request required permissions");
    }

    public void OnAppStart()
    {
        System.Diagnostics.Debug.WriteLine("Android app started");
    }

    public void OnAppStop()
    {
        System.Diagnostics.Debug.WriteLine("Android app stopped");
    }

    public void Dispose()
    {
        if (_isInitialized)
        {
            AndroidEnvironment.UnhandledExceptionRaiser -= OnAndroidUnhandledException;
            AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
            _isInitialized = false;
            System.Diagnostics.Debug.WriteLine("Android exception handler disposed");
        }
    }
}
#endif
```

### iOS-Specific Exception Handling

```csharp
// Platforms/iOS/AppDelegate.cs
#if IOS
using Foundation;
using ObjCRuntime;
using UIKit;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    private iOSExceptionHandler? _iosExceptionHandler;

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication app, NSDictionary options)
    {
        // Set up iOS-specific exception handling
        _iosExceptionHandler = new iOSExceptionHandler();
        _iosExceptionHandler.Initialize();

        return base.FinishedLaunching(app, options);
    }

    public override void WillTerminate(UIApplication application)
    {
        _iosExceptionHandler?.Dispose();
        base.WillTerminate(application);
    }

    public override void DidEnterBackground(UIApplication application)
    {
        _iosExceptionHandler?.OnAppBackground();
        base.DidEnterBackground(application);
    }

    public override void WillEnterForeground(UIApplication application)
    {
        _iosExceptionHandler?.OnAppForeground();
        base.WillEnterForeground(application);
    }
}

public class iOSExceptionHandler : IDisposable
{
    private bool _isInitialized = false;

    public void Initialize()
    {
        if (_isInitialized) return;

        // Handle Objective-C exceptions
        Runtime.MarshalObjectiveCException += OnMarshalObjectiveCException;
        Runtime.MarshalManagedException += OnMarshalManagedException;

        _isInitialized = true;
        System.Diagnostics.Debug.WriteLine("iOS exception handler initialized");
    }

    private void OnMarshalObjectiveCException(object? sender, MarshalObjectiveCExceptionEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"Objective-C exception: {e.Exception}");

            // Log to iOS system log
            var nsString = new NSString($"Objective-C Exception: {e.Exception}");
            Console.WriteLine(nsString);

            // Handle specific iOS exception types
            HandleiOSSpecificException(e.Exception);

            // Set exception mode to prevent crash
            e.ExceptionMode = MarshalObjectiveCExceptionMode.ThrowManagedException;
        }
        catch (Exception handlerEx)
        {
            System.Diagnostics.Debug.WriteLine($"Error in iOS Objective-C exception handler: {handlerEx}");
        }
    }

    private void OnMarshalManagedException(object? sender, MarshalManagedExceptionEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"Managed exception in native context: {e.Exception}");

            // Log managed exceptions that occur in native callbacks
            var nsString = new NSString($"Managed Exception in Native Context: {e.Exception}");
            Console.WriteLine(nsString);

            // Set exception mode to prevent crash
            e.ExceptionMode = MarshalManagedExceptionMode.ThrowObjectiveCException;
        }
        catch (Exception handlerEx)
        {
            System.Diagnostics.Debug.WriteLine($"Error in iOS managed exception handler: {handlerEx}");
        }
    }

    private void HandleiOSSpecificException(NSException nsException)
    {
        var exceptionName = nsException.Name;
        var reason = nsException.Reason;

        System.Diagnostics.Debug.WriteLine($"iOS Exception - Name: {exceptionName}, Reason: {reason}");

        switch (exceptionName)
        {
            case "NSInvalidArgumentException":
                HandleInvalidArgumentException(reason);
                break;
            case "NSRangeException":
                HandleRangeException(reason);
                break;
            case "NSGenericException":
                HandleGenericException(reason);
                break;
            default:
                HandleUnknownException(exceptionName, reason);
                break;
        }
    }

    private void HandleInvalidArgumentException(string? reason)
    {
        System.Diagnostics.Debug.WriteLine($"iOS Invalid Argument: {reason}");
        // Could implement argument validation recovery
    }

    private void HandleRangeException(string? reason)
    {
        System.Diagnostics.Debug.WriteLine($"iOS Range Exception: {reason}");
        // Could implement bounds checking recovery
    }

    private void HandleGenericException(string? reason)
    {
        System.Diagnostics.Debug.WriteLine($"iOS Generic Exception: {reason}");
    }

    private void HandleUnknownException(string name, string? reason)
    {
        System.Diagnostics.Debug.WriteLine($"iOS Unknown Exception - {name}: {reason}");
    }

    public void OnAppBackground()
    {
        System.Diagnostics.Debug.WriteLine("iOS app entered background");
    }

    public void OnAppForeground()
    {
        System.Diagnostics.Debug.WriteLine("iOS app entered foreground");
    }

    public void Dispose()
    {
        if (_isInitialized)
        {
            Runtime.MarshalObjectiveCException -= OnMarshalObjectiveCException;
            Runtime.MarshalManagedException -= OnMarshalManagedException;
            _isInitialized = false;
            System.Diagnostics.Debug.WriteLine("iOS exception handler disposed");
        }
    }
}
#endif
```

## MAUI Pages with Exception Handling

### ContentPage with Exception-Safe Operations

```csharp
using Plugin.ExceptionListeners;
using Microsoft.Extensions.Logging;

namespace MauiExceptionExample.Pages;

public partial class MainPage : ContentPage
{
    private readonly ILogger<MainPage> _logger;
    private readonly ExceptionSafeOperations _safeOps;

    public MainPage(ILogger<MainPage> logger)
    {
        InitializeComponent();
        _logger = logger;
        _safeOps = new ExceptionSafeOperations(_logger);
    }

    private async void OnTestHandledExceptionClicked(object sender, EventArgs e)
    {
        await _safeOps.ExecuteAsync(async () =>
        {
            throw new InvalidOperationException("This exception will be handled gracefully");
        }, "Test Handled Exception");
    }

    private async void OnTestNetworkOperationClicked(object sender, EventArgs e)
    {
        LoadingIndicator.IsVisible = true;

        var result = await _safeOps.ExecuteAsync(async () =>
        {
            // Simulate network operation that might fail
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetStringAsync("https://httpbin.org/delay/2");
            return response;
        }, "Network Operation");

        LoadingIndicator.IsVisible = false;

        if (result.IsSuccess)
        {
            await DisplayAlert("Success", "Network operation completed successfully!", "OK");
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Network operation failed", "OK");
        }
    }

    private async void OnTestBackgroundTaskClicked(object sender, EventArgs e)
    {
        // Example of proper background task handling
        _ = Task.Run(async () =>
        {
            await _safeOps.ExecuteAsync(async () =>
            {
                await Task.Delay(2000); // Simulate work

                // Randomly fail to demonstrate exception handling
                if (Random.Shared.Next(2) == 0)
                {
                    throw new InvalidDataException("Simulated background task failure");
                }

                // Update UI on main thread
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Success", "Background task completed!", "OK");
                });

            }, "Background Task");
        });

        await DisplayAlert("Info", "Background task started", "OK");
    }

    private void OnTestUnobservedTaskClicked(object sender, EventArgs e)
    {
        // This demonstrates what NOT to do - creates unobserved task exception
        Task.Run(async () =>
        {
            await Task.Delay(100);
            throw new InvalidOperationException("This will become an unobserved task exception");
        });
        // Not awaiting this task makes its exception unobserved

        DisplayAlert("Warning", "Created unobserved task - check debug output", "OK");
    }

    private async void OnTestFileOperationClicked(object sender, EventArgs e)
    {
        var result = await _safeOps.ExecuteAsync(async () =>
        {
            var fileName = Path.Combine(FileSystem.AppDataDirectory, "test.txt");
            var content = $"Test file created at {DateTimeOffset.Now}";

            await File.WriteAllTextAsync(fileName, content);
            var readContent = await File.ReadAllTextAsync(fileName);

            return readContent;
        }, "File Operation");

        if (result.IsSuccess)
        {
            await DisplayAlert("Success", $"File operation result: {result.Data}", "OK");
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "File operation failed", "OK");
        }
    }
}

public class ExceptionSafeOperations
{
    private readonly ILogger _logger;

    public ExceptionSafeOperations(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<OperationResult<T>> ExecuteAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting operation: {OperationName}", operationName);

            var result = await operation();

            _logger.LogInformation("Operation completed successfully: {OperationName}", operationName);
            return OperationResult<T>.Success(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Operation was cancelled: {OperationName}", operationName);
            return OperationResult<T>.Failure("Operation was cancelled");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error in operation: {OperationName}", operationName);
            return OperationResult<T>.Failure("Network connection error. Please check your internet connection.");
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Timeout in operation: {OperationName}", operationName);
            return OperationResult<T>.Failure("The operation took too long to complete. Please try again.");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied in operation: {OperationName}", operationName);
            return OperationResult<T>.Failure("Access denied. Please check your permissions.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in operation: {OperationName}", operationName);
            return OperationResult<T>.Failure("An unexpected error occurred. Please try again.");
        }
    }

    public async Task<OperationResult> ExecuteAsync(
        Func<Task> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        var wrappedOperation = async () =>
        {
            await operation();
            return true; // Return dummy value for non-generic version
        };

        var result = await ExecuteAsync(wrappedOperation, operationName, cancellationToken);
        return new OperationResult(result.IsSuccess, result.ErrorMessage);
    }
}

public class OperationResult<T>
{
    public bool IsSuccess { get; private set; }
    public T? Data { get; private set; }
    public string? ErrorMessage { get; private set; }

    private OperationResult(bool isSuccess, T? data, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorMessage = errorMessage;
    }

    public static OperationResult<T> Success(T data) => new(true, data, null);
    public static OperationResult<T> Failure(string errorMessage) => new(false, default, errorMessage);
}

public class OperationResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }

    public OperationResult(bool isSuccess, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static OperationResult Success() => new(true, null);
    public static OperationResult Failure(string errorMessage) => new(false, errorMessage);
}
```

## App Center Integration Example

### Complete App Center Crash Reporting Setup

```csharp
// Add NuGet package: Microsoft.AppCenter.Crashes

using Microsoft.AppCenter;
using Microsoft.AppCenter.Crashes;
using Plugin.ExceptionListeners;
using Plugin.ExceptionListeners.Listeners;
using Plugin.ExceptionListeners.Maui;

public class AppCenterExceptionIntegration : IDisposable
{
    private readonly List<ExceptionListener> _listeners = new();
    private readonly string _appCenterSecret;

    public AppCenterExceptionIntegration(string appCenterSecret)
    {
        _appCenterSecret = appCenterSecret;
    }

    public void Initialize()
    {
        // Initialize App Center
        AppCenter.Start(_appCenterSecret, typeof(Crashes));

        // Set up exception listeners
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleUnhandledException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException));
        _listeners.Add(new NativeUnhandledExceptionListener(HandleNativeException));

        // Configure App Center callbacks
        Crashes.ShouldProcessErrorReport = ShouldProcessErrorReport;
        Crashes.ShouldAwaitUserConfirmation = ShouldAwaitUserConfirmation;
        Crashes.GetErrorAttachments = GetErrorAttachments;

        System.Diagnostics.Debug.WriteLine("App Center exception integration initialized");
    }

    private void HandleUnhandledException(object? sender, ExceptionEventArgs e)
    {
        // Let App Center handle the crash reporting
        var properties = new Dictionary<string, string>
        {
            ["Source"] = sender?.GetType().Name ?? "Unknown",
            ["ExceptionType"] = e.Exception.GetType().Name,
            ["Platform"] = DeviceInfo.Platform.ToString(),
            ["AppVersion"] = AppInfo.VersionString,
            ["DeviceModel"] = DeviceInfo.Model
        };

        Crashes.TrackError(e.Exception, properties);
    }

    private void HandleTaskException(object? sender, ExceptionEventArgs e)
    {
        var properties = new Dictionary<string, string>
        {
            ["Source"] = "TaskScheduler",
            ["ExceptionType"] = e.Exception.GetType().Name,
            ["Platform"] = DeviceInfo.Platform.ToString()
        };

        Crashes.TrackError(e.Exception, properties);
    }

    private void HandleNativeException(object? sender, ExceptionEventArgs e)
    {
        var properties = new Dictionary<string, string>
        {
            ["Source"] = "NativePlatform",
            ["ExceptionType"] = e.Exception.GetType().Name,
            ["Platform"] = DeviceInfo.Platform.ToString(),
            ["IsNativeException"] = (e.Exception is NativeUnhandledException).ToString()
        };

        Crashes.TrackError(e.Exception, properties);
    }

    private bool ShouldProcessErrorReport(ErrorReport report)
    {
        // Process all error reports
        System.Diagnostics.Debug.WriteLine($"Processing error report: {report.Id}");
        return true;
    }

    private bool ShouldAwaitUserConfirmation()
    {
        // In production, you might want to ask users before sending crash reports
        // For this example, always send automatically
        return false;
    }

    private IEnumerable<ErrorAttachmentLog> GetErrorAttachments(ErrorReport report)
    {
        var attachments = new List<ErrorAttachmentLog>();

        try
        {
            // Attach application logs if available
            var logPath = Path.Combine(FileSystem.AppDataDirectory, "app.log");
            if (File.Exists(logPath))
            {
                var logContent = File.ReadAllText(logPath);
                attachments.Add(ErrorAttachmentLog.AttachmentWithText(logContent, "app.log"));
            }

            // Attach device information
            var deviceInfo = new
            {
                Model = DeviceInfo.Model,
                Manufacturer = DeviceInfo.Manufacturer,
                Platform = DeviceInfo.Platform.ToString(),
                OSVersion = DeviceInfo.VersionString,
                AppVersion = AppInfo.VersionString,
                Timestamp = DateTimeOffset.UtcNow
            };

            var deviceInfoJson = System.Text.Json.JsonSerializer.Serialize(deviceInfo, new JsonSerializerOptions { WriteIndented = true });
            attachments.Add(ErrorAttachmentLog.AttachmentWithText(deviceInfoJson, "device-info.json"));

        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating attachments: {ex}");
        }

        return attachments;
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

// Usage in App.xaml.cs
public partial class App : Application
{
    private AppCenterExceptionIntegration? _appCenterIntegration;

    public App()
    {
        InitializeComponent();

        // Initialize App Center integration
        var appCenterSecret = GetAppCenterSecret();
        _appCenterIntegration = new AppCenterExceptionIntegration(appCenterSecret);
        _appCenterIntegration.Initialize();

        MainPage = new AppShell();
    }

    private string GetAppCenterSecret()
    {
        // Return platform-specific App Center secrets
        // You can get these from App Center portal after creating your app
#if ANDROID
        return "your-android-app-center-secret";
#elif IOS
        return "your-ios-app-center-secret";
#elif WINDOWS
        return "your-windows-app-center-secret";
#else
        return "your-generic-app-center-secret";
#endif
    }

    protected override void CleanUp()
    {
        _appCenterIntegration?.Dispose();
        base.CleanUp();
    }
}
```

These comprehensive MAUI examples demonstrate how to implement robust exception handling across all supported platforms, with proper lifecycle management, platform-specific handling, and integration with popular crash reporting services.
