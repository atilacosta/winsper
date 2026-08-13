using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsDictation.Core;

namespace WindowsDictation.App.Windows;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => PositionIndicator();
    }

    public void ShowState(RecordingState state, string? message)
    {
        StateDot.Fill = new SolidColorBrush(ColorFor(state));
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

