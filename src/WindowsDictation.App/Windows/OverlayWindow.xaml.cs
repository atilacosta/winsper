using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsDictation.Core;

namespace WindowsDictation.App.Windows;

public partial class OverlayWindow : Window
{
    private bool showWhenIdle = true;
    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => PositionIndicator();
    }

    public void ShowState(RecordingState state, string? message)
    {
        if (state == RecordingState.Idle && !showWhenIdle)
        {
            Hide();
            return;
        }

        bool showMessage = state == RecordingState.Error;
        Width = showMessage ? 300 : 42;
        Height = showMessage ? 48 : 42;
        StatusText.Visibility = showMessage ? Visibility.Visible : Visibility.Collapsed;
        StateDot.HorizontalAlignment = showMessage ? System.Windows.HorizontalAlignment.Left : System.Windows.HorizontalAlignment.Center;
        StateDot.Margin = showMessage ? new Thickness(14, 0, 0, 0) : new Thickness(0);
        StateDot.Fill = new SolidColorBrush(ColorFor(state));
        StatusText.Text = showMessage ? message ?? "Dictation couldn't finish. Try again." : string.Empty;
        Opacity = state == RecordingState.Idle ? 0.72 : 0.96;
        ApplyAnimation(state);

        if (!IsVisible)
        {
            Show();
        }

        PositionIndicator();
    }

    public void SetPosition(IndicatorPosition position)
    {
        Tag = position;
        PositionIndicator();
    }

    public void SetShowWhenIdle(bool value)
    {
        showWhenIdle = value;
    }

    private void PositionIndicator()
    {
        IndicatorPosition position = Tag is IndicatorPosition configured
            ? configured
            : IndicatorPosition.TopCenter;
        Rect workArea = SystemParameters.WorkArea;
        double margin = 18;

        Left = position switch
        {
            IndicatorPosition.TopLeft or IndicatorPosition.BottomLeft => workArea.Left + margin,
            IndicatorPosition.TopRight or IndicatorPosition.BottomRight => workArea.Right - Width - margin,
            _ => workArea.Left + (workArea.Width - Width) / 2
        };
        Top = position switch
        {
            IndicatorPosition.BottomCenter or IndicatorPosition.BottomLeft or IndicatorPosition.BottomRight
                => workArea.Bottom - Height - margin,
            _ => workArea.Top + margin
        };
    }

    private void ApplyAnimation(RecordingState state)
    {
        if (FindResource("DotPulse") is not Storyboard storyboard)
        {
            return;
        }

        storyboard.Remove(this);
        storyboard.Begin(this, true);
    }

    private static System.Windows.Media.Color ColorFor(RecordingState state)
    {
        return state switch
        {
            RecordingState.Recording => System.Windows.Media.Color.FromRgb(248, 113, 113),
            RecordingState.Transcribing => System.Windows.Media.Color.FromRgb(251, 191, 36),
            RecordingState.Inserting => System.Windows.Media.Color.FromRgb(52, 211, 153),
            RecordingState.Error => System.Windows.Media.Color.FromRgb(244, 63, 94),
            _ => System.Windows.Media.Color.FromRgb(125, 211, 252)
        };
    }

}

