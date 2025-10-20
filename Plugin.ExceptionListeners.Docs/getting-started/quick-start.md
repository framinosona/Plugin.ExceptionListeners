# Quick Start Guide

Get up and running with Plugin.ExceptionListeners in just a few minutes.

## Basic Setup

### Step 1: Install the Package

For standard .NET applications:

```bash
dotnet add package Plugin.ExceptionListeners
```

For MAUI applications (includes core package):

```bash
dotnet add package Plugin.ExceptionListeners.Maui
```

### Step 2: Add Using Statements

```csharp
using Plugin.ExceptionListeners;
using Plugin.ExceptionListeners.Listeners;

// For MAUI applications, also add:
using Plugin.ExceptionListeners.Maui;
```

### Step 3: Create Your Exception Handler

```csharp
private void HandleException(object? sender, ExceptionEventArgs e)
{
    // Log the exception
    Console.WriteLine($"Exception caught: {e.Exception.Message}");

    // Optionally log stack trace for debugging
    Console.WriteLine($"Stack trace: {e.Exception.StackTrace}");

    // Add your custom logic here:
    // - Send to logging service
    // - Report to crash analytics
    // - Show user-friendly error message
    // - etc.
}
```

### Step 4: Set Up Exception Listeners

Choose the listeners that match your needs:

```csharp
// Listen for first-chance exceptions (catches all exceptions as they occur)
using var firstChanceListener = new CurrentDomainFirstChanceExceptionListener(HandleException);

// Listen for unhandled exceptions (catches exceptions that would crash the app)
using var unhandledListener = new CurrentDomainUnhandledExceptionListener(HandleException);

// Listen for unobserved task exceptions
using var taskListener = new TaskSchedulerUnobservedTaskExceptionListener(HandleException);

// Your application code here...
Console.WriteLine("Exception listeners are active!");

// Simulate an exception to test
try
{
    throw new InvalidOperationException("Test exception");
}
catch
{
    // This will be caught by firstChanceListener
}

Console.ReadKey(); // Keep console open to see output
```

## MAUI Quick Start

For MAUI applications, add native exception handling in your `App.xaml.cs`:

### Step 1: Modify App.xaml.cs

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

        MainPage = new AppShell();
    }

    private void SetupExceptionHandling()
    {
        // Handle .NET exceptions
        _listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleException));
        _listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleException));

        // Handle native platform exceptions
        _listeners.Add(new NativeUnhandledExceptionListener(HandleNativeException));
    }

    private void HandleException(object? sender, ExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($".NET Exception: {e.Exception.Message}");
        // Add your logging/reporting logic here
    }

    private void HandleNativeException(object? sender, ExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Native Exception: {e.Exception.Message}");

        // Handle native exceptions specially if needed
        if (e.Exception is NativeUnhandledException nativeEx)
        {
            // This is a wrapped native exception
            System.Diagnostics.Debug.WriteLine($"Original native error: {nativeEx.InnerException?.Message}");
        }
    }

    protected override void OnSleep()
    {
        // Clean up listeners when app goes to sleep
        foreach (var listener in _listeners)
        {
            listener.Dispose();
        }
        _listeners.Clear();

        base.OnSleep();
    }
}
```

## Console Application Example

Here's a complete console application example:

```csharp
using Plugin.ExceptionListeners;
using Plugin.ExceptionListeners.Listeners;

namespace ExceptionListenerDemo
{
    class Program
    {
        private static readonly List<ExceptionListener> Listeners = new();

        static async Task Main(string[] args)
        {
            Console.WriteLine("Setting up exception listeners...");

            SetupExceptionHandling();

            Console.WriteLine("Exception listeners are active!");
            Console.WriteLine("Testing various exception scenarios...\n");

            // Test scenarios
            await TestFirstChanceException();
            await TestUnobservedTaskException();
            // Note: Testing unhandled exceptions would terminate the app

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();

            // Clean up
            CleanupListeners();
        }

        private static void SetupExceptionHandling()
        {
            Listeners.Add(new CurrentDomainFirstChanceExceptionListener(HandleFirstChanceException));
            Listeners.Add(new CurrentDomainUnhandledExceptionListener(HandleUnhandledException));
            Listeners.Add(new TaskSchedulerUnobservedTaskExceptionListener(HandleTaskException));
        }

        private static void HandleFirstChanceException(object? sender, ExceptionEventArgs e)
        {
            Console.WriteLine($"[FirstChance] {e.Exception.GetType().Name}: {e.Exception.Message}");
        }

        private static void HandleUnhandledException(object? sender, ExceptionEventArgs e)
        {
            Console.WriteLine($"[Unhandled] {e.Exception.GetType().Name}: {e.Exception.Message}");
        }

        private static void HandleTaskException(object? sender, ExceptionEventArgs e)
        {
            Console.WriteLine($"[Task] {e.Exception.GetType().Name}: {e.Exception.Message}");
        }

        private static async Task TestFirstChanceException()
        {
            Console.WriteLine("Testing first-chance exception...");
            try
            {
                throw new InvalidOperationException("This is a test exception");
            }
            catch
            {
                // Exception is caught, so it won't be unhandled
                Console.WriteLine("Exception was caught and handled.\n");
            }
        }

        private static async Task TestUnobservedTaskException()
        {
            Console.WriteLine("Testing unobserved task exception...");

            // Create a task that throws but don't await it
            var task = Task.Run(() => throw new InvalidDataException("Unobserved task exception"));

            // Don't await the task, let it become unobserved
            await Task.Delay(100); // Give it time to fail

            // Force garbage collection to trigger unobserved task exception
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            await Task.Delay(100); // Give time for the event to fire
            Console.WriteLine("Unobserved task exception should have been caught.\n");
        }

        private static void CleanupListeners()
        {
            foreach (var listener in Listeners)
            {
                listener.Dispose();
            }
            Listeners.Clear();
        }
    }
}
```

## Testing Your Setup

To verify that your exception listeners are working:

### 1. Test First-Chance Exceptions

```csharp
try
{
    throw new Exception("Test exception");
}
catch
{
    // This will trigger the first-chance listener
}
```

### 2. Test Unobserved Task Exceptions

```csharp
// Create a task that fails but don't await it
Task.Run(() => throw new Exception("Unobserved exception"));

// Force garbage collection after a delay
await Task.Delay(100);
GC.Collect();
GC.WaitForPendingFinalizers();
```

### 3. Test Native Exceptions (MAUI only)

Native exceptions are typically triggered by:

- Platform-specific API failures
- Memory access violations
- Native library crashes
- Platform permission denials

These are automatically caught by the `NativeUnhandledExceptionListener`.

## Next Steps

Now that you have basic exception handling set up:

1. [Learn about different listener types](../guides/exception-listeners.md)
2. [Explore advanced configuration options](configuration.md)
3. [See more comprehensive examples](../examples/basic-usage.md)
4. [Review best practices](../guides/best-practices.md)

## Common Issues

### Listeners Not Triggering

- Ensure listeners are kept in scope (not garbage collected)
- Verify exception handlers don't throw exceptions themselves
- Check that you're testing the right type of exception for each listener

### Performance Concerns

- First-chance exceptions can be very frequent; keep handlers lightweight
- Consider filtering exceptions in high-frequency scenarios
- Use appropriate listener types for your use case

### Memory Leaks

- Always dispose listeners when done (use `using` statements when possible)
- Don't keep strong references to listeners longer than needed
- Be careful with lambda captures in exception handlers
