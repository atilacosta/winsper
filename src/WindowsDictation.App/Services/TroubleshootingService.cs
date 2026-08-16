using WindowsDictation.Core;

namespace WindowsDictation.App.Services;

public sealed record TroubleshootingIssue(string Title, string Message, string Recovery);

public interface ITroubleshootingService
{
    TroubleshootingIssue? CurrentIssue { get; }
    event EventHandler? IssueChanged;
    string GetOverlayMessage(RecordingState state, string? technicalMessage);
    void ReportHotkeyConflict();
}

public sealed class TroubleshootingService : ITroubleshootingService
{
    public TroubleshootingIssue? CurrentIssue { get; private set; }
    public event EventHandler? IssueChanged;

    public string GetOverlayMessage(RecordingState state, string? technicalMessage)
    {
        if (state != RecordingState.Error)
        {
            return technicalMessage switch
            {
                "No audio captured" or "No speech detected" => "No speech detected. Try speaking a little closer.",
                _ => technicalMessage ?? "Ready"
            };
        }

        TroubleshootingIssue issue = Map(technicalMessage);
        CurrentIssue = issue;
        IssueChanged?.Invoke(this, EventArgs.Empty);
        return $"{issue.Title}. {issue.Recovery}";
    }

    public void ReportHotkeyConflict()
    {
        CurrentIssue = new TroubleshootingIssue(
            "Hotkey unavailable",
            "Your selected hotkey is already used by another app or Windows shortcut.",
            "Choose a different hotkey in Settings.");
        IssueChanged?.Invoke(this, EventArgs.Empty);
    }

    private static TroubleshootingIssue Map(string? technicalMessage)
    {
        string message = technicalMessage ?? string.Empty;
        if (message.Contains("hotkey", StringComparison.OrdinalIgnoreCase) || message.Contains("register", StringComparison.OrdinalIgnoreCase))
        {
            return new TroubleshootingIssue("Hotkey unavailable", "Your selected hotkey is already used by another app or Windows shortcut.", "Choose a different hotkey in Settings.");
        }

        if (message.Contains("insert", StringComparison.OrdinalIgnoreCase) || message.Contains("clipboard", StringComparison.OrdinalIgnoreCase))
        {
            return new TroubleshootingIssue("Text wasn't inserted", "Your transcription is safely available in Recent transcriptions.", "Open Settings to copy it.");
        }

        if (message.Contains("model", StringComparison.OrdinalIgnoreCase) || message.Contains("whisper", StringComparison.OrdinalIgnoreCase) || message.Contains("download", StringComparison.OrdinalIgnoreCase))
        {
            return new TroubleshootingIssue("Speech model couldn't start", "The selected model could not be loaded.", "Try again or choose a smaller model in Settings.");
        }

        if (message.Contains("wave", StringComparison.OrdinalIgnoreCase) || message.Contains("device", StringComparison.OrdinalIgnoreCase) || message.Contains("microphone", StringComparison.OrdinalIgnoreCase))
        {
            return new TroubleshootingIssue("Microphone unavailable", "We couldn't access the selected microphone.", "Choose another microphone in Settings.");
        }

        return new TroubleshootingIssue("Dictation couldn't finish", "Something interrupted this recording.", "Try again. If it continues, check Settings.");
    }
}
