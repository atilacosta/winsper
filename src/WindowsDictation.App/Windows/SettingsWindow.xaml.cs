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

    public SettingsWindow(
        ISettingsStore settingsStore,
        AudioDeviceCatalog audioDeviceCatalog,
        IModelManager modelManager,
        GlobalHotkeyService hotkeyService,
        StartupRegistrationService startupRegistration,
        OverlayWindow overlayWindow)
    {
        this.settingsStore = settingsStore;
        this.audioDeviceCatalog = audioDeviceCatalog;
        this.modelManager = modelManager;
        this.hotkeyService = hotkeyService;
        this.startupRegistration = startupRegistration;
        this.overlayWindow = overlayWindow;

        InitializeComponent();
        Loaded += (_, _) => Populate();
    }

    private void Populate()
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
            LaunchOnStartup = LaunchOnStartupCheckBox.IsChecked == true
        };

        try
        {
            hotkeyService.Register(settings.Hotkey);
            startupRegistration.Apply(settings.LaunchOnStartup);
            await settingsStore.SaveAsync(settings, CancellationToken.None);
            overlayWindow.SetPosition(settings.IndicatorPosition);
            Close();
        }
        catch (Win32Exception exception)
        {
            System.Windows.MessageBox.Show(this, exception.Message, "Hotkey Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CancelClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public sealed record ComboOption<TValue>(string Label, TValue Value);


