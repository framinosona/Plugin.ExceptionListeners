namespace Plugin.ExceptionListeners.Maui;

/// <summary>
/// Represents an exception that wraps an unhandled native/platform-specific exception surfaced to .NET.
/// </summary>
/// <remarks>
/// This exception is typically thrown when a native (Android/iOS/MacCatalyst/Windows) unhandled exception
/// is intercepted and rethrown within the managed environment to preserve the original context.
/// </remarks>
public class NativeUnhandledException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NativeUnhandledException"/> class.
    /// </summary>
    public NativeUnhandledException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NativeUnhandledException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public NativeUnhandledException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NativeUnhandledException"/> class with a specified
    /// error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public NativeUnhandledException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
