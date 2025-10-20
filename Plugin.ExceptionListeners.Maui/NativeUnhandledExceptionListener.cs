namespace Plugin.ExceptionListeners.Maui;

/// <summary>
///     Listens for unhandled exceptions on native platforms and forwards them to the managed exception handler.
/// </summary>
public class NativeUnhandledExceptionListener : ExceptionListener
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="NativeUnhandledExceptionListener"/> class.
    /// </summary>
    /// <param name="received">The event handler to invoke when an exception is received.</param>
    public NativeUnhandledExceptionListener(EventHandler<ExceptionEventArgs> received) : base(received)
    {
#if IOS || MACCATALYST
        Runtime.MarshalObjectiveCException += RuntimeOnMarshalObjectiveCException;
        Runtime.MarshalManagedException += RuntimeOnMarshalManagedException;
#endif

#if ANDROID
        AndroidEnvironment.UnhandledExceptionRaiser += AndroidEnvironmentOnUnhandledExceptionRaiser;
#endif

#if WINDOWS
        Windows.ApplicationModel.Core.CoreApplication.UnhandledErrorDetected += OnUnhandledErrorDetected;
#endif
    }

    /// <summary>
    ///     Releases the unmanaged resources used by the <see cref="NativeUnhandledExceptionListener"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
#if IOS || MACCATALYST
            Runtime.MarshalObjectiveCException -= RuntimeOnMarshalObjectiveCException;
            Runtime.MarshalManagedException -= RuntimeOnMarshalManagedException;
#endif

#if ANDROID
            AndroidEnvironment.UnhandledExceptionRaiser -= AndroidEnvironmentOnUnhandledExceptionRaiser;
#endif

#if WINDOWS
            Windows.ApplicationModel.Core.CoreApplication.UnhandledErrorDetected -= OnUnhandledErrorDetected;
#endif
        }
        base.Dispose(disposing);
    }

#if IOS || MACCATALYST
    private void RuntimeOnMarshalManagedException(object? sender, MarshalManagedExceptionEventArgs e)
    {
        OnReceived(sender, e.Exception);
    }

    private void RuntimeOnMarshalObjectiveCException(object? sender, MarshalObjectiveCExceptionEventArgs e)
    {
        OnReceived(sender, new ObjCException(e.Exception));
    }
#endif

#if ANDROID
    private void AndroidEnvironmentOnUnhandledExceptionRaiser(object? sender, RaiseThrowableEventArgs e)
    {
        // The point of listeners is to handle the Exception and prevent crashes. Not propagating the exception. Stop listening if you want it to crash your application.
        e.Handled = true;

        // Wrap Android-specific exceptions in our native exception type for better categorization
        if (e.Exception.GetType().FullName?.StartsWith("Java.Lang.", StringComparison.Ordinal) == true)
        {
            OnReceived(sender, new NativeUnhandledException($"Android native exception: {e.Exception.Message}", e.Exception));
        }
        else
        {
            OnReceived(sender, e.Exception);
        }
    }
#endif

#if WINDOWS
    private void OnUnhandledErrorDetected(object? sender, UnhandledErrorDetectedEventArgs e)
    {
        try
        {
            // Try to get more detailed error information
            var errorInfo = e.UnhandledError.ToString() ?? "A native unhandled error occurred.";
            OnReceived(sender, new NativeUnhandledException(errorInfo));
        }
        catch (InvalidOperationException ex)
        {
            // Fallback if we can't extract error details due to invalid operation
            OnReceived(sender, new NativeUnhandledException("A native unhandled error occurred.", ex));
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            // Fallback for COM-related errors in Windows
            OnReceived(sender, new NativeUnhandledException("A native COM error occurred.", ex));
        }
        catch (UnauthorizedAccessException ex)
        {
            // Fallback for access-related errors
            OnReceived(sender, new NativeUnhandledException("A native access error occurred.", ex));
        }
    }
#endif
}
