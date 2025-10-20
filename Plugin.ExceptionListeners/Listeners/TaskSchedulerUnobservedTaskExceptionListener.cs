namespace Plugin.ExceptionListeners.Listeners;


/// <summary>
/// Listens for unobserved task exceptions via <see cref="TaskScheduler.UnobservedTaskException"/> and handles them using the provided event handler.
/// </summary>
public class TaskSchedulerUnobservedTaskExceptionListener : ExceptionListener
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaskSchedulerUnobservedTaskExceptionListener"/> class and subscribes to <see cref="TaskScheduler.UnobservedTaskException"/>.
    /// </summary>
    /// <param name="received">The event handler to invoke when an unobserved task exception occurs.</param>
    public TaskSchedulerUnobservedTaskExceptionListener(EventHandler<ExceptionEventArgs> received) : base(received)
    {
        TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="TaskSchedulerUnobservedTaskExceptionListener"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            TaskScheduler.UnobservedTaskException -= TaskSchedulerOnUnobservedTaskException;
        }
        base.Dispose(disposing);
    }

    private void TaskSchedulerOnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // The point of listeners is to handle the Exception and prevent crashes. Not propagating the exception. Stop listening if you want it to crash your application.
        e.SetObserved();
        OnReceived(sender, e.Exception.GetBaseException());
    }
}
