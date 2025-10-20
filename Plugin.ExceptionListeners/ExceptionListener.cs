namespace Plugin.ExceptionListeners;

/// <summary>
///     Provides a base class for exception listeners that handle and propagate exceptions via events.
/// </summary>
public abstract class ExceptionListener : IDisposable
{
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExceptionListener"/> class.
    /// </summary>
    /// <param name="received">The event handler to invoke when an exception is received.</param>
    protected ExceptionListener(EventHandler<ExceptionEventArgs> received)
    {
        Received = received;
    }

    /// <summary>
    ///     Occurs when an exception is received by the listener.
    /// </summary>
    public event EventHandler<ExceptionEventArgs>? Received;

    /// <summary>
    ///     Invokes the <see cref="Received"/> event with the specified sender and exception.
    /// </summary>
    /// <param name="sender">The sender of the event.</param>
    /// <param name="exception">The exception to propagate.</param>
    protected void OnReceived(object? sender, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception, nameof(exception));
        Received?.Invoke(sender, new ExceptionEventArgs(exception));
    }

    /// <summary>
    ///     Releases resources used by the listener and unsubscribes all event handlers.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Releases the unmanaged resources used by the <see cref="ExceptionListener"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Received = null;
            }
            _disposed = true;
        }
    }
}
