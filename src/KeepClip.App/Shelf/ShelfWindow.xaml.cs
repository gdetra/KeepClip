using KeepClip.App.Appearance;
using KeepClip.App.Interop;
using KeepClip.Application;
using KeepClip.Application.Appearance;
using KeepClip.Application.Paste;
using KeepClip.Domain.History;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;

namespace KeepClip.App.Shelf;

public sealed partial class ShelfWindow : Window
{
    private readonly HistoryService _history;
    private readonly ShelfViewModel _model;
    private readonly PasteClip _paste;
    private readonly PasteSession _session = new();
    private readonly OutsideClickWatcher _outsideClicks;
    private readonly WindowTheme _theme;
    private PasteRequest? _continuation;
    private bool _busy, _dialogOpen, _exiting;
    private NativeMethods.Rect _workArea;
    private double _displayScale = 1;
    private int _shelfWidth;

    public ShelfViewModel Model => _model;

    public bool IsVisible { get; private set; }

    public event Action? SettingsRequested;

    public event Action<string>? Notification;

    public ShelfWindow(HistoryService history, ShelfViewModel model, PasteClipFactory pasteFactory)
    {
        _model = model;
        InitializeComponent();
        _history = history;
        Root.DataContext = _model;
        _theme = new(this, Root);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "KeepClip.ico"));
        _outsideClicks = new(
            WinRT.Interop.WindowNative.GetWindowHandle(this),
            () => IsVisible && !_dialogOpen,
            () =>
            {
                var request = _session.Snapshot();
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_session.IsCurrent(request) && IsVisible && !_dialogOpen)
                    {
                        Hide();
                    }
                });
            });
        _paste = pasteFactory.Create(() =>
        {
            Hide();
            _continuation = _session.Snapshot();
        });
        var presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(true, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Closing += (_, args) =>
        {
            if (!_exiting)
            {
                args.Cancel = true;
                Hide();
            }
        };
        Activated += (_, args) =>
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated && !_dialogOpen)
            {
                Hide();
            }
        };
        history.HistoryChanged += OnHistoryChanged;
        _model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(_model.Entries))
            {
                UpdateCount();
            }
        };
        OnHistoryChanged();
        Status.RegisterPropertyChangedCallback(InfoBar.IsOpenProperty, (_, _) => ResizeShelf());
        Status.SizeChanged += (_, _) => ResizeShelf();
    }

    public void ApplyAppearance(AppearanceSettings? appearance) => _theme.Apply(appearance);

    private void OnHistoryChanged() => DispatcherQueue.TryEnqueue(() =>
    {
        _model.Refresh(_history.Entries);
        UpdateCount();
    });

    private void UpdateCount()
    {
        CountLabel.Text = $"{_history.Entries.Count}/200 · Invio incolla · Esc chiude";
        EmptyState.Visibility = _model.Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = _history.Entries.Count == 0 ? "Copia qualcosa per iniziare" : "Nessun elemento trovato";
    }

    public void ShowFor(nint target)
    {
        _session.Open(target);
        _model.Query = "";
        _model.Refresh(_history.Entries);
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var monitor = NativeMethods.MonitorFromWindow(target != 0 ? target : hwnd, 2);
        var info = new NativeMethods.MonitorInfo
        {
            Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfo>()
        };
        if (NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            // Move our own per-monitor aware HWND onto the target monitor before reading its DPI.
            AppWindow.Move(new PointInt32(info.Work.Left + 16, info.Work.Top + 16));
            var dpi = NativeMethods.GetDpiForWindow(hwnd);
            var scale = (dpi == 0 ? 96 : dpi) / 96d;
            var w = Math.Min(info.Work.Right - info.Work.Left - 32, (int)(1200 * scale));
            _workArea = info.Work;
            _displayScale = scale;
            _shelfWidth = w;
            ResizeShelf();
            CountLabel.Visibility = w / scale < 1000 ? Visibility.Collapsed : Visibility.Visible;
        }

        IsVisible = true;
        _outsideClicks.Start();
        AppWindow.Show();
        Activate();
        Search.Focus(FocusState.Programmatic);
    }

    private void ResizeShelf()
    {
        if (_shelfWidth <= 0)
        {
            return;
        }

        // Measure all rows at their required height. An open InfoBar must add
        // window space rather than taking space away from the cards.
        Root.InvalidateMeasure();
        Root.Measure(new Windows.Foundation.Size(_shelfWidth / _displayScale, double.PositiveInfinity));
        var height = Math.Min(_workArea.Bottom - _workArea.Top - 32, (int)Math.Ceiling((Root.DesiredSize.Height + 8) * _displayScale));
        var bounds = new RectInt32(
            _workArea.Left + (_workArea.Right - _workArea.Left - _shelfWidth) / 2,
            _workArea.Bottom - height - 16,
            _shelfWidth,
            height);
        if (AppWindow.Size.Width != bounds.Width || AppWindow.Size.Height != bounds.Height || AppWindow.Position.Y != bounds.Y)
        {
            AppWindow.MoveAndResize(bounds);
        }
    }

    public void Hide()
    {
        if (!IsVisible)
        {
            return;
        }

        _session.Cancel();
        IsVisible = false;
        _outsideClicks.Stop();
        AppWindow.Hide();
    }

    public void ShowMessage(string message)
    {
        Status.Message = message;
        Status.IsOpen = true;
    }

    public void ExitWindow()
    {
        _exiting = true;
        _outsideClicks.Dispose();
        _history.HistoryChanged -= OnHistoryChanged;
        Close();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        SettingsRequested?.Invoke();
    }

    private async void Card_PasteRequested(object? sender, ClipCardEventArgs e)
    {
        _model.SelectedEntry = e.Entry;
        await PasteAsync(e.Entry);
    }

    private async Task PasteAsync(ClipEntry entry)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        var request = _session.Snapshot();
        try
        {
            _continuation = request;
            var result = await _paste.ExecuteAsync(entry, request.Destination, () => _continuation is { } current && _session.IsCurrent(current));
            if (result is null)
            {
                return;
            }

            if (result.Failure == PasteFailure.OriginalFilesUnavailable)
            {
                ShowMessage("Uno o più file originali non sono più disponibili. Ripristinali oppure rimuovi questa card.");
                return;
            }

            if (result.Message is not null)
            {
                Notification?.Invoke(result.Message);
            }
        }
        catch (Exception)
        {
            ShowMessage("Impossibile copiare questo elemento. Verifica che il contenuto sia ancora disponibile.");
            Notification?.Invoke("Impossibile copiare l'elemento selezionato.");
        }
        finally
        {
            _continuation = null;
            _busy = false;
        }
    }

    private async Task MutateAsync(Func<Task> action)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            await action();
        }
        catch
        {
            ShowMessage("Impossibile salvare la modifica. La cronologia è rimasta invariata.");
        }
        finally
        {
            _busy = false;
        }
    }

    private async void Card_PinRequested(
        object? sender,
        ClipCardEventArgs e) => await MutateAsync(() => _history.TogglePinAsync(e.Entry.Id));

    private async void Card_DeleteRequested(
        object? sender,
        ClipCardEventArgs e) => await MutateAsync(() => _history.DeleteAsync(e.Entry.Id));

    private async void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (_dialogOpen || _busy)
        {
            return;
        }

        _dialogOpen = true;
        try
        {
            var include = new CheckBox
            {
                Content = "Elimina anche i preferiti"
            };
            var dialog = new ContentDialog
            {
                XamlRoot = Root.XamlRoot,
                RequestedTheme = Root.RequestedTheme,
                Title = "Svuotare la cronologia?",
                Content = include,
                PrimaryButtonText = "Svuota",
                CloseButtonText = "Annulla",
                DefaultButton = ContentDialogButton.Close
            };
            _theme.StyleDialog(dialog);
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await MutateAsync(() => _history.ClearAsync(include.IsChecked == true));
            }
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private async void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_dialogOpen)
        {
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            Hide();
            e.Handled = true;
        }
        // Enter/Space on a card action must remain an action, not a paste.
        else if (FocusManager.GetFocusedElement(Root.XamlRoot) is ButtonBase)
        {
            return;
        }
        else if (e.Key == VirtualKey.Enter && _model.SelectedEntry is { } entry)
        {
            e.Handled = true;
            await PasteAsync(entry);
        }
        else if (e.Key is VirtualKey.Down or VirtualKey.Up || (e.Key is VirtualKey.Left or VirtualKey.Right && Search.FocusState == FocusState.Unfocused))
        {
            var index = _model.Entries.ToList().FindIndex(x => x.Id == _model.SelectedEntry?.Id);
            var delta = e.Key is VirtualKey.Left or VirtualKey.Up ? -1 : 1;
            if (_model.Entries.Count > 0)
            {
                _model.SelectedEntry = _model.Entries[Math.Clamp(index + delta, 0, _model.Entries.Count - 1)];
                Cards.ScrollIntoView(_model.SelectedEntry);
            }

            e.Handled = true;
        }
    }
}
