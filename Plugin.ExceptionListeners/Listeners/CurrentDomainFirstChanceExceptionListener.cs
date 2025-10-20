using System.Runtime.ExceptionServices;

namespace Plugin.ExceptionListeners.Listeners;

/// <summary>
/// Listens for first-chance exceptions on the current AppDomain and handles them using the provided event handler.
/// </summary>
public class CurrentDomainFirstChanceExceptionListener : ExceptionListener
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CurrentDomainFirstChanceExceptionListener"/> class and subscribes to the <c>FirstChanceException</c> event.
    /// </summary>
    /// <param name="received">The event handler to invoke when a first-chance exception occurs.</param>
    public CurrentDomainFirstChanceExceptionListener(EventHandler<ExceptionEventArgs> received) : base(received)
    {
        AppDomain.CurrentDomain.FirstChanceException += CurrentDomainOnFirstChanceException;
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="CurrentDomainFirstChanceExceptionListener"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AppDomain.CurrentDomain.FirstChanceException -= CurrentDomainOnFirstChanceException;
        }
        base.Dispose(disposing);
    }

    private void CurrentDomainOnFirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
    {
        OnReceived(sender, e.Exception);
    }
}
