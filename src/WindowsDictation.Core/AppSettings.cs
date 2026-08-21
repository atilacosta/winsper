namespace WindowsDictation.Core;

public sealed class AppSettings
{
    public HotkeySettings Hotkey { get; init; } = HotkeySettings.Default;
    public string? MicrophoneDeviceId { get; init; }
    public ModelKind SelectedModel { get; init; } = ModelKind.TinyEnglish;
    public InsertionMode InsertionMode { get; init; } = InsertionMode.ClipboardPasteThenUnicodeFallback;
    public IndicatorPosition IndicatorPosition { get; init; } = IndicatorPosition.TopCenter;
    public bool ShowIndicatorWhenIdle { get; init; } = true;
    public bool LaunchOnStartup { get; init; }
}

public sealed class HotkeySettings
{
    public static HotkeySettings Default { get; } = new()
    {
        Control = true,
        Key = "Space"
    };

    public bool Control { get; init; }
    public bool Alt { get; init; }
    public bool Shift { get; init; }
    public bool Windows { get; init; }
    public string Key { get; init; } = "Space";
}

public enum IndicatorPosition
{
    TopCenter,
    TopLeft,
    TopRight,
    BottomCenter,
    BottomLeft,
    BottomRight
}
