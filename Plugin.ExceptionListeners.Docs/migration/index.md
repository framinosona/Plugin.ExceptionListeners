# Migration Guide

This guide helps you migrate from other exception handling solutions to Plugin.ExceptionListeners or upgrade between versions.

## Migrating from Manual Exception Handling

### Before: Manual AppDomain Exception Handling

```csharp
// Old approach - manual exception handling
public class OldExceptionHandler
{
    public void Initialize()
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            LogException("Unhandled", exception);
        }
    }

    private void OnFirstChanceException(object sender, FirstChanceExceptionEventArgs e)
    {
        LogException("FirstChance", e.Exception);
    }

    private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException("Task", e.Exception);
        e.SetObserved(); // Prevent application termination
    }

    private void LogException(string type, Exception exception)
    {
        Console.WriteLine($"{type}: {exception}");
    }

    public void Cleanup()
    {
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        AppDomain.CurrentDomain.FirstChanceException -= OnFirstChanceException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
    }
}
```

### After: Using Plugin.ExceptionListeners

```csharp
// New approach - using Plugin.ExceptionListeners
using Plugin.ExceptionListeners;
using Plugin.ExceptionListeners.Listeners;

public class NewExceptionHandler : IDisposable
{
    private readonly List<ExceptionListener> _listeners = new();

    public void Initialize()
    {
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleException));
        _listeners.Add(new CurrentDomainFirstChanceExceptionListener(HandleException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleException));
    }

    private void HandleException(object? sender, ExceptionEventArgs e)
    {
        var sourceType = sender?.GetType().Name ?? "Unknown";
        Console.WriteLine($"{sourceType}: {e.Exception}");
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

### Migration Benefits

1. **Unified Interface**: All exception types use the same `ExceptionEventArgs` interface
2. **Automatic Cleanup**: Implements `IDisposable` for proper resource management
3. **Consistent Behavior**: UnobservedTaskExceptionListener automatically calls `SetObserved()`
4. **Type Safety**: Strongly typed listener classes prevent registration errors
5. **Extensibility**: Easy to add custom listeners by inheriting from `ExceptionListener`

## Migrating from Third-Party Libraries

### From Serilog's SelfLog

```csharp
// Old: Using Serilog's SelfLog
using Serilog.Debugging;

public void SetupOldLogging()
{
    SelfLog.Enable(Console.Error);
    // Only captures Serilog internal errors
}

// New: Plugin.ExceptionListeners with Serilog integration
using Plugin.ExceptionListeners.Listeners;
using Serilog;

public class SerilogExceptionHandler : IDisposable
{
    private readonly ILogger _logger;
    private readonly List<ExceptionListener> _listeners = new();

    public SerilogExceptionHandler()
    {
        _logger = Log.ForContext<SerilogExceptionHandler>();

        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleUnhandledException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException));
    }

    private void HandleUnhandledException(object? sender, ExceptionEventArgs e)
    {
        _logger.Fatal(e.Exception, "Unhandled exception from {Source}", sender?.GetType().Name);
    }

    private void HandleTaskException(object? sender, ExceptionEventArgs e)
    {
        _logger.Error(e.Exception, "Unobserved task exception");
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

### From NLog's GlobalExceptionHandler

```csharp
// Old: NLog GlobalExceptionHandler
using NLog;

public class OldNLogHandler
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public void Setup()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            Logger.Fatal(e.ExceptionObject as Exception, "Unhandled exception");
        };
    }
}

// New: Plugin.ExceptionListeners with NLog
using Plugin.ExceptionListeners.Listeners;
using NLog;

public class NewNLogHandler : IDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly List<ExceptionListener> _listeners = new();

    public void Setup()
    {
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleException));
    }

    private void HandleException(object? sender, ExceptionEventArgs e)
    {
        Logger.Error(e.Exception, "Exception from {Source}: {ExceptionType}",
            sender?.GetType().Name ?? "Unknown",
            e.Exception.GetType().Name);
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

## Migrating MAUI Applications

### From Xamarin.Forms Exception Handling

```csharp
// Old: Xamarin.Forms approach
public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Old manual approach
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        MainPage = new MainPage();
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // Manual handling
        System.Diagnostics.Debug.WriteLine($"Unhandled: {e.ExceptionObject}");
    }

    private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Task: {e.Exception}");
        e.SetObserved();
    }
}
```

```csharp
// New: MAUI with Plugin.ExceptionListeners
using Plugin.ExceptionListeners.Listeners;
using Plugin.ExceptionListeners.Maui;

public partial class App : Application
{
    private readonly List<ExceptionListener> _listeners = new();

    public App()
    {
        InitializeComponent();

        // New unified approach
        InitializeExceptionHandling();

        MainPage = new AppShell();
    }

    private void InitializeExceptionHandling()
    {
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleException));
        _listeners.Add(new NativeUnhandledExceptionListener(HandleNativeException));
    }

    private void HandleException(object? sender, ExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Exception: {e.Exception}");
    }

    private void HandleNativeException(object? sender, ExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Native: {e.Exception}");
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

### Platform-Specific Migration

#### Android Migration

```csharp
// Old: Manual Android exception handling
#if ANDROID
[Application]
public class MainApplication : MauiApplication
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override void OnCreate()
    {
        base.OnCreate();

        // Old manual approach
        AndroidEnvironment.UnhandledExceptionRaiser += (sender, e) =>
        {
            Android.Util.Log.Error("MyApp", $"Unhandled: {e.Exception}");
            e.Handled = true;
        };
    }
}
#endif

// New: Using NativeUnhandledExceptionListener
#if ANDROID
[Application]
public class MainApplication : MauiApplication
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override void OnCreate()
    {
        base.OnCreate();

        // NativeUnhandledExceptionListener automatically handles AndroidEnvironment.UnhandledExceptionRaiser
        // No additional setup needed - handled by the MAUI package
    }
}
#endif
```

#### iOS Migration

```csharp
// Old: Manual iOS exception handling
#if IOS
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication app, NSDictionary options)
    {
        // Old manual approach
        Runtime.MarshalObjectiveCException += (sender, e) =>
        {
            Console.WriteLine($"Objective-C: {e.Exception}");
        };

        Runtime.MarshalManagedException += (sender, e) =>
        {
            Console.WriteLine($"Managed: {e.Exception}");
        };

        return base.FinishedLaunching(app, options);
    }
}
#endif

// New: Using NativeUnhandledExceptionListener
#if IOS
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication app, NSDictionary options)
    {
        // NativeUnhandledExceptionListener automatically handles Runtime exceptions
        // No additional setup needed - handled by the MAUI package

        return base.FinishedLaunching(app, options);
    }
}
#endif
```

## Version Migration Guide

### Migrating from 1.x to 2.x (Future)

When migrating between major versions, follow these steps:

1. **Update Package References**

   ```xml
   <!-- Old -->
   <PackageReference Include="Plugin.ExceptionListeners" Version="1.0.0" />

   <!-- New -->
   <PackageReference Include="Plugin.ExceptionListeners" Version="2.0.0" />
   ```

2. **Update Namespace Imports**

   ```csharp
   // If namespaces change in future versions
   using Plugin.ExceptionListeners.V2;
   using Plugin.ExceptionListeners.V2.Listeners;
   ```

3. **Check for Breaking Changes**
   - Review the changelog for any breaking API changes
   - Update method signatures if changed
   - Verify event handling patterns

## Migration Checklist

Use this checklist when migrating to Plugin.ExceptionListeners:

### Pre-Migration

- [ ] Identify all current exception handling code
- [ ] Document existing exception handling behavior
- [ ] Note any custom exception handling logic
- [ ] Backup current implementation

### During Migration

- [ ] Install Plugin.ExceptionListeners packages
- [ ] Replace manual AppDomain event subscriptions
- [ ] Replace TaskScheduler event subscriptions
- [ ] Add NativeUnhandledExceptionListener for MAUI apps
- [ ] Update exception handler method signatures
- [ ] Implement IDisposable pattern for cleanup

### Post-Migration

- [ ] Test all exception scenarios
- [ ] Verify exception logging still works
- [ ] Check performance impact (especially for first-chance exceptions)
- [ ] Update documentation and comments
- [ ] Train team on new API usage

### MAUI-Specific Checklist

- [ ] Remove manual platform-specific exception handling
- [ ] Test on all target platforms
- [ ] Verify native exceptions are caught
- [ ] Update crash reporting integration
- [ ] Test app lifecycle scenarios (suspend/resume)

## Common Migration Issues

### Issue 1: Performance Impact

**Problem**: First-chance exception listeners causing performance issues.

**Solution**:

```csharp
// Only enable in debug builds or with explicit configuration
#if DEBUG
var firstChanceListener = new CurrentDomainFirstChanceExceptionListener(HandleFirstChance);
#endif

// Or use configuration-based enabling
if (Configuration.GetValue<bool>("EnableFirstChanceLogging"))
{
    var firstChanceListener = new CurrentDomainFirstChanceExceptionListener(HandleFirstChance);
}
```

### Issue 2: Missing Native Exceptions

**Problem**: Native exceptions not being caught after migration.

**Solution**: Ensure you're using the MAUI package and NativeUnhandledExceptionListener:

```csharp
// Make sure to install Plugin.ExceptionListeners.Maui package
// and add the native listener
var nativeListener = new NativeUnhandledExceptionListener(HandleNativeException);
```

### Issue 3: Memory Leaks

**Problem**: Exception listeners not being disposed properly.

**Solution**: Implement proper disposal pattern:

```csharp
public class ExceptionManager : IDisposable
{
    private readonly List<IDisposable> _listeners = new();
    private bool _disposed = false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            foreach (var listener in _listeners)
            {
                listener?.Dispose();
            }
            _listeners.Clear();
            _disposed = true;
        }
    }
}
```

### Issue 4: Exception Handler Exceptions

**Problem**: Exception handlers themselves throwing exceptions.

**Solution**: Wrap handlers in try-catch:

```csharp
private void HandleException(object? sender, ExceptionEventArgs e)
{
    try
    {
        // Your exception handling logic
        ProcessException(e.Exception);
    }
    catch (Exception handlerEx)
    {
        // Fallback logging that shouldn't fail
        System.Diagnostics.Debug.WriteLine($"Handler failed: {handlerEx}");
    }
}
```

## Getting Help

If you encounter issues during migration:

1. Check the [GitHub Issues](https://github.com/framinosona/Plugin.ExceptionListeners/issues)
2. Review the [API documentation](../api/index.md)
3. Look at [example implementations](../examples/basic-usage.md)
4. Create a new issue with:
   - Your current implementation
   - Target migration approach
   - Specific error messages
   - Platform and framework versions

## Best Practices After Migration

1. **Use Configuration**: Make exception handling configurable
2. **Test Thoroughly**: Verify all exception scenarios work
3. **Monitor Performance**: Watch for any performance regressions
4. **Document Changes**: Update team documentation
5. **Plan Rollback**: Have a rollback plan if issues arise

This migration guide should help you smoothly transition to Plugin.ExceptionListeners while maintaining robust exception handling in your applications.
