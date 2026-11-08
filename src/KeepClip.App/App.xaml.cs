using KeepClip.App.Clipboard;
using KeepClip.App.Composition;
using KeepClip.App.Desktop;
using KeepClip.App.Interop;
using KeepClip.App.Settings;
using KeepClip.App.Shelf;
using KeepClip.Application;
using KeepClip.Application.Settings;
using KeepClip.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace KeepClip.App;

public partial class App : Microsoft.UI.Xaml.Application
{
    private readonly SingleInstanceService _instance = new();
    private ServiceProvider? _services;
    private ClipboardMonitor? _monitor;
    private IHotkeySettingsService? _hotkey;
    private TrayIconService? _tray;
    private ShelfWindow? _shelf;
    private SettingsWindow? _settingsWindow;
    private HistoryService? _history;
    private AppSettings _initialSettings = AppSettings.Default;
    private SettingsCoordinator? _settingsCoordinator;

    private ForegroundTracker? _foreground;
    private SelectionCopyService? _selectionCopy;
    private string _dataPath = "";
    private bool _storageReady;
    private bool _shuttingDown;

    private AppSettings CurrentSettings => _settingsCoordinator?.CurrentSettings ?? _initialSettings;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (!_instance.TryAcquire())
        {
            Exit();
            return;
        }

        try
        {
            try
            {
                _dataPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            }
            catch
            {
                _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeepClip");
            }

            _services = await AppServiceRegistration.CreateAsync(_dataPath, new AppCallbacks(
                () => CurrentSettings,
                () => CurrentSettings.CopyOnSelection && _storageReady && _history?.IsPaused == false,
                OnCopied,
                Notify,
                OpenShelf,
                ApplyRuntimeSettings,
                RecoverAsync));
            var startup = _services.GetRequiredService<StartupSettings>();
            _initialSettings = startup.Settings;
            var startupError = startup.Error;
            _history = _services.GetRequiredService<HistoryService>();
            _history.SetPaused(CurrentSettings.IsPaused);
            _foreground = _services.GetRequiredService<ForegroundTracker>();
            _shelf = _services.GetRequiredService<ShelfWindow>();
            _shelf.ApplyAppearance(CurrentSettings.Appearance);
            _hotkey = _services.GetRequiredService<IHotkeySettingsService>();
            _tray = _services.GetRequiredService<TrayIconService>();
            _tray.Paused = CurrentSettings.IsPaused;
            _settingsCoordinator = _services.GetRequiredService<SettingsCoordinator>();
            _tray.Open += OpenShelf;
            _tray.Settings += OpenSettings;
            _tray.TogglePause += TogglePause;
            _tray.Exit += Shutdown;
            _tray.Show();
            _shelf.SettingsRequested += OpenSettings;
            _shelf.Notification += Notify;
            if (!_hotkey.TryRegister(CurrentSettings.Hotkey))
            {
                startupError = "Scorciatoia occupata. Apri le impostazioni dall'icona e scegli un'altra combinazione.";
            }

            try
            {
                await _history.InitializeAsync();
                _storageReady = true;
            }
            catch (Exception ex)
            {
                startupError = ex is HistoryStorageException ? ex.Message : "Impossibile aprire la cronologia. Controlla la cartella dati nelle impostazioni.";
                _history.SetPaused(true);
            }

            _monitor = _services.GetRequiredService<ClipboardMonitor>();
            _monitor.Problem += Notify;
            _monitor.Copied += OnCopied;
            _selectionCopy = _services.GetRequiredService<SelectionCopyService>();
            _selectionCopy.Problem += Notify;
            if (!UpdateSelectionCapture())
            {
                startupError = "Impossibile attivare la copia alla selezione. Le copie manuali restano disponibili.";
            }

            if (!_hotkey.SetWindowsV(CurrentSettings.UseWindowsV))
            {
                startupError ??= "Win+V non disponibile. Usa la scorciatoia alternativa di KeepClip.";
            }

            var startupActivation = false;
            try
            {
                startupActivation = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs().Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.StartupTask;
            }
            catch
            {
            }

            if (!StartupCommand.ShouldStartHidden(Environment.GetCommandLineArgs(), startupActivation))
            {
                _shelf.ShowFor(0);
            }

            if (startupError is not null)
            {
                Notify(startupError);
            }
            else if (CurrentSettings.IsPaused)
            {
                _shelf.ShowMessage("La cattura è in pausa. Riprendila dall'icona nell'area di notifica.");
            }
        }
        catch (Exception)
        {
            Shutdown();
        }
    }

    private void OpenShelf()
    {
        if (_shelf is null)
        {
            return;
        }

        if (_shelf.IsVisible)
        {
            _shelf.Hide();
            return;
        }

        _shelf.ShowFor(_foreground?.Capture() ?? new WindowTarget().Capture());
    }

    private void OpenSettings()
    {
        if (_services is null)
        {
            return;
        }

        _shelf?.Hide();
        if (_settingsWindow is null)
        {
            _settingsWindow = _services.GetRequiredService<SettingsWindow>();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.Activate();
    }

    private async void TogglePause()
    {
        if (_settingsCoordinator is null)
        {
            return;
        }

        var result = await _settingsCoordinator.TogglePauseAsync();
        Notify(result.Status == SettingsApplyStatus.Applied ? result.EffectiveSettings.IsPaused ? "Cattura in pausa." : "Cattura ripresa." : SettingsApplyMessages.ForSave(result));
    }

    private SettingsApplyWarnings ApplyRuntimeSettings(AppSettings settings)
    {
        var warnings = SettingsApplyWarnings.None;
        _history?.SetPaused(settings.IsPaused || !_storageReady);
        if (_tray is not null)
        {
            _tray.Paused = settings.IsPaused || !_storageReady;
        }

        _shelf?.ApplyAppearance(settings.Appearance);
        if (!UpdateSelectionCapture())
        {
            warnings |= SettingsApplyWarnings.SelectionCaptureUnavailable;
        }

        if (!_storageReady)
        {
            warnings |= SettingsApplyWarnings.StorageUnavailable;
        }

        return warnings;
    }

    private async Task RecoverAsync()
    {
        if (_history is null)
        {
            return;
        }

        _history.SetPaused(true);
        _storageReady = false;
        UpdateSelectionCapture();
        // Wait for any in-flight capture to finish before archiving the database and images.
        await _history.ResetWithBackupAsync(_services!.GetRequiredService<HistoryRecovery>().PrepareResetAsync);
        _storageReady = true;
        _history.SetPaused(CurrentSettings.IsPaused);
        if (_tray is not null)
        {
            _tray.Paused = CurrentSettings.IsPaused;
        }

        UpdateSelectionCapture();
    }

    private void Notify(string message)
    {
        _shelf?.ShowMessage(message);
        _tray?.Notify(message);
    }

    private void OnCopied()
    {
        if (!_shuttingDown && CurrentSettings.CopySound && _history?.IsPaused == false)
        {
            CopyFeedbackSound.Play(CurrentSettings.CopySoundVolume);
        }
    }

    private bool UpdateSelectionCapture()
    {
        if (_selectionCopy is null)
        {
            return true;
        }

        if (CurrentSettings.CopyOnSelection && _storageReady && _history?.IsPaused == false)
        {
            return _selectionCopy.Start();
        }

        _selectionCopy.Stop();
        return true;
    }

    private void Shutdown()
    {
        if (_shuttingDown)
        {
            return;
        }

        _shuttingDown = true;
        CopyFeedbackSound.Stop();
        _selectionCopy?.Stop();
        _settingsWindow?.Close();
        _shelf?.ExitWindow();
        _services?.Dispose();
        _instance.Dispose();
        Exit();
    }
}
