using System.Windows;

namespace VoiceButton;

public partial class App : System.Windows.Application
{
    private const string InstanceMutexName = @"Local\VoiceButton.SingleInstance.8F9A61F0";
    private const string ActivationEventName = @"Local\VoiceButton.Activate.8F9A61F0";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _activationEvent;
    private RegisteredWaitHandle? _activationRegistration;
    private bool _ownsInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            SignalExistingInstance();
            _instanceMutex.Dispose();
            _instanceMutex = null;
            Shutdown();
            return;
        }

        _ownsInstanceMutex = true;
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        _activationRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            OnActivationRequested,
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activationRegistration?.Unregister(null);
        _activationEvent?.Dispose();

        if (_ownsInstanceMutex)
        {
            try
            {
                _instanceMutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The mutex was already released during shutdown.
            }
        }

        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void OnActivationRequested(object? state, bool timedOut)
    {
        if (timedOut)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (MainWindow is MainWindow window)
            {
                window.ShowFromExternalLaunch();
            }
        });
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var activationEvent = EventWaitHandle.OpenExisting(ActivationEventName);
            activationEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // The first instance is still starting; the mutex still prevents a duplicate.
        }
        catch (UnauthorizedAccessException)
        {
            // A protected instance still wins ownership, so this process exits.
        }
    }
}
