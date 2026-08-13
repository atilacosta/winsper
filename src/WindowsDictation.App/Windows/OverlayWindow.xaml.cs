using System.Windows;
using System.Windows.Media;
using WindowsDictation.Core;

namespace WindowsDictation.App.Windows;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => PositionTopCenter();
    }

    public void ShowState(RecordingState state, string? message)
    {
        StateText.Text = state switch
        {
            RecordingState.Idle => "Ready",
            RecordingState.Recording => "Recording",
            RecordingState.Transcribing => "Transcribing",
            RecordingState.Inserting => "Pasting",
            RecordingState.Error => "Needs attention",
            _ => state.ToString()
        };

        MessageText.Text = string.IsNullOrWhiteSpace(message) ? "Ctrl+Space" : message;
        StateDot.Fill = new SolidColorBrush(ColorFor(state));
        Opacity = state == RecordingState.Idle ? 0.72 : 0.96;

        if (!IsVisible)
        {
            Show();
        }

        PositionTopCenter();
    }

    private void PositionTopCenter()
    {
        Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
        Top = SystemParameters.WorkArea.Top + 16;
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

