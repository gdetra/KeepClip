using System.Runtime.InteropServices;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeepClip.Application;
using KeepClip.Application.Appearance;
using KeepClip.Application.Settings;

namespace KeepClip.App.Settings;

internal sealed partial class SettingsViewModel : ObservableObject
{
    private readonly Func<AppSettings, Task<ApplySettingsResult>> _apply;
    private readonly Func<Task<StartupState>> _readStartup;
    private readonly Func<Task> _recover;

    public SettingsViewModel(
        AppSettings settings,
        string dataPath,
        Func<AppSettings, Task<ApplySettingsResult>> apply,
        Func<Task<StartupState>> readStartup,
        Func<Task> recover)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(readStartup);
        ArgumentNullException.ThrowIfNull(recover);
        _apply = apply;
        _readStartup = readStartup;
        _recover = recover;
        DataPathLabel = $"Dati locali: {dataPath}";
        UseWindowsV = settings.UseWindowsV;
        ModifierIndex = settings.Hotkey.Modifiers switch
        {
            3 => 1,
            5 => 2,
            _ => 0
        };
        Letter = ((char)settings.Hotkey.Key).ToString();
        CaptureEnabled = !settings.IsPaused;
        AutoStart = settings.AutoStart;
        CopyOnSelection = settings.CopyOnSelection;
        CopySound = settings.CopySound;
        SoundVolume = Math.Clamp(settings.CopySoundVolume, 0, 100);
        SetAppearance(ThemeCatalog.Normalize(settings.Appearance));
    }

    [ObservableProperty]
    public partial bool UseWindowsV { get; set; }

    [ObservableProperty]
    public partial int ModifierIndex { get; set; }

    [ObservableProperty]
    public partial string Letter { get; set; } = "V";

    [ObservableProperty]
    public partial bool CaptureEnabled { get; set; }

    [ObservableProperty]
    public partial bool AutoStart { get; set; }

    [ObservableProperty]
    public partial bool StartupAvailable { get; set; }

    [ObservableProperty]
    public partial string StartupHint { get; set; } = "";

    [ObservableProperty]
    public partial bool CopyOnSelection { get; set; }

    [ObservableProperty]
    public partial bool CopySound { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SoundVolumeLabel))]
    public partial double SoundVolume { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Appearance))]
    public partial int ThemeIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Appearance))]
    public partial string SelectedPalette { get; set; } = "coral";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Appearance))]
    public partial bool ColorSurfaces { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "";

    [ObservableProperty]
    public partial bool IsStatusOpen { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(RecoverCommand))]
    [NotifyPropertyChangedFor(nameof(CanChangeSettings))]
    public partial bool IsBusy { get; private set; }

    public string DataPathLabel { get; }

    public string SoundVolumeLabel => $"{Volume}%";

    public AppearanceSettings Appearance => ThemeCatalog.Normalize(new((ThemeMode)ThemeIndex, SelectedPalette, ColorSurfaces));

    private int Volume => double.IsFinite(SoundVolume) ? (int)Math.Round(Math.Clamp(SoundVolume, 0, 100)) : 50;

    public bool CanChangeSettings => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanChangeSettings))]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var submittedLetter = Letter;
        var key = submittedLetter.Trim().ToUpperInvariant();
        if (key.Length != 1 || key[0] is < 'A' or > 'Z')
        {
            Show("Inserisci una lettera da A a Z.");
            return;
        }

        IsBusy = true;
        try
        {
            var requested = CreateSettings(key[0]);
            var result = await _apply(requested);
            Show(SettingsApplyMessages.ForSave(result));
            if (result.Status != SettingsApplyStatus.Rejected)
            {
                ApplyEffectiveSettings(requested, result, submittedLetter);
            }
        }
        catch (Exception error)
        {
            if (!IsExpectedFailure(error))
            {
                throw;
            }

            Show("Impossibile salvare le impostazioni.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private AppSettings CreateSettings(char key) => new(
        Hotkey: new Hotkey(ModifierIndex switch
        {
            1 => 3u,
            2 => 5u,
            _ => 6u
        }, key),
        IsPaused: !CaptureEnabled,
        AutoStart: AutoStart,
        CopyOnSelection: CopyOnSelection,
        CopySound: CopySound,
        CopySoundVolume: Volume,
        Appearance: Appearance)
    {
        UseWindowsV = UseWindowsV,
    };

    private void ApplyEffectiveSettings(AppSettings requested, ApplySettingsResult result, string submittedLetter)
    {
        // Preserve edits made while the asynchronous save was in progress.
        if (Letter == submittedLetter && ModifierIndex == GetModifierIndex(requested.Hotkey))
        {
            Letter = ((char)result.EffectiveSettings.Hotkey.Key).ToString();
            ModifierIndex = GetModifierIndex(result.EffectiveSettings.Hotkey);
        }

        if (AutoStart == requested.AutoStart)
        {
            AutoStart = result.EffectiveSettings.AutoStart;
        }

        if (result.Startup is { } startup)
        {
            StartupAvailable = startup.Available;
            StartupHint = startup.Message ?? "L'avvio automatico segue anche le impostazioni di Windows.";
        }
    }

    private static int GetModifierIndex(Hotkey hotkey) => hotkey.Modifiers switch
    {
        3 => 1,
        5 => 2,
        _ => 0
    };

    [RelayCommand]
    private async Task RefreshStartupAsync()
    {
        try
        {
            var state = await _readStartup();
            StartupAvailable = state.Available;
            AutoStart = state.Enabled;
            StartupHint = state.Message ?? "L'avvio automatico segue anche le impostazioni di Windows.";
        }
        catch (Exception error)
        {
            if (!IsExpectedFailure(error))
            {
                throw;
            }

            StartupAvailable = false;
            StartupHint = "Impossibile leggere l'avvio automatico per questo account Windows.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanChangeSettings))]
    private async Task RecoverAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _recover();
            Show("Cronologia ripristinata. Il backup è nella cartella dati.");
        }
        catch (Exception error)
        {
            if (!IsExpectedFailure(error))
            {
                throw;
            }

            Show("Ripristino non riuscito. I file disponibili sono stati preservati nella cartella dati.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ResetTheme() => SetAppearance(new());

    [RelayCommand]
    private void SelectPalette(string palette) => SelectedPalette = palette;

    public void Show(string message)
    {
        StatusMessage = message;
        IsStatusOpen = true;
    }

    private void SetAppearance(AppearanceSettings appearance)
    {
        ThemeIndex = (int)appearance.Mode;
        SelectedPalette = appearance.Palette;
        ColorSurfaces = appearance.ColorSurfaces;
    }

    private static bool IsExpectedFailure(
        Exception error) => error is IOException or UnauthorizedAccessException or COMException or JsonException or System.Security.SecurityException;
}
