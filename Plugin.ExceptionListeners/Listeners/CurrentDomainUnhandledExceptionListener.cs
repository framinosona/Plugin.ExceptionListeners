namespace Plugin.ExceptionListeners.Listeners;

/// <summary>
/// Listens for unhandled exceptions on the current AppDomain and handles them using the provided event handler.
/// </summary>
public class CurrentDomainUnhandledExceptionListener : ExceptionListener
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CurrentDomainUnhandledExceptionListener"/> class and subscribes to the <c>UnhandledException</c> event.
    /// </summary>
    /// <param name="received">The event handler to invoke when an unhandled exception occurs.</param>
    public CurrentDomainUnhandledExceptionListener(EventHandler<ExceptionEventArgs> received) : base(received)
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="CurrentDomainUnhandledExceptionListener"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomainOnUnhandledException;
        }
        base.Dispose(disposing);
    }

    private void CurrentDomainOnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            OnReceived(sender, exception.GetBaseException());
        }
        else
        {
            OnReceived(sender, new InvalidOperationException($"Unknown exception : {e.ExceptionObject}"));
        }
    }
}
