using System.ComponentModel;
using System.Windows;
using WindowsDictation.App.Services;
using WindowsDictation.Core;

namespace WindowsDictation.App.Windows;

public partial class SettingsWindow : Window
{
    private readonly ISettingsStore settingsStore;
    private readonly AudioDeviceCatalog audioDeviceCatalog;
    private readonly IModelManager modelManager;
    private readonly GlobalHotkeyService hotkeyService;
    private readonly StartupRegistrationService startupRegistration;
    private readonly OverlayWindow overlayWindow;
    private readonly ITranscriptionHistoryStore transcriptionHistory;
    private readonly ITroubleshootingService troubleshooting;
    private readonly WhisperNetTranscriptionEngine transcriptionEngine;

    public SettingsWindow(
        ISettingsStore settingsStore,
        AudioDeviceCatalog audioDeviceCatalog,
        IModelManager modelManager,
        GlobalHotkeyService hotkeyService,
        StartupRegistrationService startupRegistration,
        OverlayWindow overlayWindow,
        ITranscriptionHistoryStore transcriptionHistory,
        ITroubleshootingService troubleshooting,
        WhisperNetTranscriptionEngine transcriptionEngine)
    {
        this.settingsStore = settingsStore;
        this.audioDeviceCatalog = audioDeviceCatalog;
        this.modelManager = modelManager;
        this.hotkeyService = hotkeyService;
        this.startupRegistration = startupRegistration;
        this.overlayWindow = overlayWindow;
        this.transcriptionHistory = transcriptionHistory;
        this.troubleshooting = troubleshooting;
        this.transcriptionEngine = transcriptionEngine;

        InitializeComponent();
        Loaded += async (_, _) => await PopulateAsync();
    }

    private async Task PopulateAsync()
    {
        AppSettings settings = settingsStore.Current;

        MicrophoneComboBox.ItemsSource = audioDeviceCatalog.GetInputDevices();
        MicrophoneComboBox.SelectedValue = settings.MicrophoneDeviceId ?? "0";

        ModelComboBox.ItemsSource = modelManager.SupportedModels
            .Select(model => new ComboOption<ModelKind>(model.DisplayName, model.Kind))
            .ToArray();
        ModelComboBox.SelectedValue = settings.SelectedModel;

        InsertionModeComboBox.ItemsSource =
        new[] {
            new ComboOption<InsertionMode>("Paste, then Unicode fallback", InsertionMode.ClipboardPasteThenUnicodeFallback),
            new ComboOption<InsertionMode>("Paste only", InsertionMode.ClipboardPasteOnly),
            new ComboOption<InsertionMode>("Unicode injection only", InsertionMode.UnicodeOnly)
        };
        InsertionModeComboBox.SelectedValue = settings.InsertionMode;

        IndicatorPositionComboBox.ItemsSource = new[]
        {
            new ComboOption<IndicatorPosition>("Top center", IndicatorPosition.TopCenter),
            new ComboOption<IndicatorPosition>("Top left", IndicatorPosition.TopLeft),
            new ComboOption<IndicatorPosition>("Top right", IndicatorPosition.TopRight),
            new ComboOption<IndicatorPosition>("Bottom center", IndicatorPosition.BottomCenter),
            new ComboOption<IndicatorPosition>("Bottom left", IndicatorPosition.BottomLeft),
            new ComboOption<IndicatorPosition>("Bottom right", IndicatorPosition.BottomRight)
        };
        IndicatorPositionComboBox.SelectedValue = settings.IndicatorPosition;

        ControlCheckBox.IsChecked = settings.Hotkey.Control;
        AltCheckBox.IsChecked = settings.Hotkey.Alt;
        ShiftCheckBox.IsChecked = settings.Hotkey.Shift;
        WindowsCheckBox.IsChecked = settings.Hotkey.Windows;
        HotkeyKeyComboBox.SelectedValue = settings.Hotkey.Key;

        LaunchOnStartupCheckBox.IsChecked = settings.LaunchOnStartup;
        ShowIndicatorWhenIdleCheckBox.IsChecked = settings.ShowIndicatorWhenIdle;

        IReadOnlyList<TranscriptionHistoryEntry> entries = await transcriptionHistory.GetAllAsync(CancellationToken.None);
        TranscriptionHistoryItems.ItemsSource = entries;
        EmptyHistoryText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowTroubleshootingIssue();
    }

    private async void SaveClicked(object sender, RoutedEventArgs e)
    {
        AppSettings current = settingsStore.Current;
        HotkeySettings hotkey = new()
        {
            Control = ControlCheckBox.IsChecked == true,
            Alt = AltCheckBox.IsChecked == true,
            Shift = ShiftCheckBox.IsChecked == true,
            Windows = WindowsCheckBox.IsChecked == true,
            Key = HotkeyKeyComboBox.SelectedValue as string ?? "Space"
        };

        if (!hotkey.Control && !hotkey.Alt && !hotkey.Shift && !hotkey.Windows)
        {
            System.Windows.MessageBox.Show(this, "Choose at least one modifier key.", "Invalid Hotkey", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AppSettings settings = new()
        {
            Hotkey = hotkey,
            MicrophoneDeviceId = MicrophoneComboBox.SelectedValue as string,
            SelectedModel = ModelComboBox.SelectedValue is ModelKind model ? model : ModelKind.BaseEnglish,
            InsertionMode = InsertionModeComboBox.SelectedValue is InsertionMode mode
                ? mode
                : InsertionMode.ClipboardPasteThenUnicodeFallback,
            IndicatorPosition = IndicatorPositionComboBox.SelectedValue is IndicatorPosition position
                ? position
                : current.IndicatorPosition,
            ShowIndicatorWhenIdle = ShowIndicatorWhenIdleCheckBox.IsChecked == true,
            LaunchOnStartup = LaunchOnStartupCheckBox.IsChecked == true
        };

        try
        {
            hotkeyService.Register(settings.Hotkey);
            startupRegistration.Apply(settings.LaunchOnStartup);
            await settingsStore.SaveAsync(settings, CancellationToken.None);
            overlayWindow.SetPosition(settings.IndicatorPosition);
            overlayWindow.SetShowWhenIdle(settings.ShowIndicatorWhenIdle);
            overlayWindow.ShowState(RecordingState.Idle, "Ready");
            Close();
        }
        catch (Win32Exception)
        {
            troubleshooting.ReportHotkeyConflict();
            ShowTroubleshootingIssue();
        }
    }

    private void CancelClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CopyTranscriptClicked(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: TranscriptionHistoryEntry entry } button)
        {
            System.Windows.Clipboard.SetText(entry.Text);
            button.Content = "Copied";
        }
    }

    private async void RetryTroubleshootingClicked(object sender, RoutedEventArgs e)
    {
        if (troubleshooting.CurrentIssue?.Action != TroubleshootingAction.RetryModel)
        {
            return;
        }

        ModelKind model = ModelComboBox.SelectedValue is ModelKind selected
            ? selected
            : settingsStore.Current.SelectedModel;

        TroubleshootingRetryButton.IsEnabled = false;
        TroubleshootingRetryButton.Content = "Retrying...";
        try
        {
            await transcriptionEngine.WarmupAsync(model, CancellationToken.None);
            troubleshooting.Clear();
            overlayWindow.ShowState(RecordingState.Idle, "Model ready");
            ShowTroubleshootingIssue();
        }
        catch (Exception exception)
        {
            overlayWindow.ShowState(RecordingState.Error,
                troubleshooting.GetOverlayMessage(RecordingState.Error, exception.Message));
            ShowTroubleshootingIssue();
        }
        finally
        {
            TroubleshootingRetryButton.IsEnabled = true;
            TroubleshootingRetryButton.Content = "Try again";
        }
    }

    private void ShowTroubleshootingIssue()
    {
        TroubleshootingIssue? issue = troubleshooting.CurrentIssue;
        TroubleshootingCard.Visibility = issue is null ? Visibility.Collapsed : Visibility.Visible;
        TroubleshootingRetryButton.Visibility = issue?.Action == TroubleshootingAction.RetryModel
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (issue is not null)
        {
            TroubleshootingTitle.Text = issue.Title;
            TroubleshootingMessage.Text = issue.Message;
            TroubleshootingRecovery.Text = issue.Recovery;
        }
    }
}

public sealed record ComboOption<TValue>(string Label, TValue Value);


