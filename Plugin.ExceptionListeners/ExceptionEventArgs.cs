namespace Plugin.ExceptionListeners;

/// <summary>
///     Event arguments carrying an Exception instance.
/// </summary>
public sealed class ExceptionEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ExceptionEventArgs"/> class.
    /// </summary>
    /// <param name="exception">The exception associated with the event.</param>
    public ExceptionEventArgs(Exception exception)
    {
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
    }

    /// <summary>
    ///     Gets the exception associated with the event.
    /// </summary>
    public Exception Exception { get; }
}
