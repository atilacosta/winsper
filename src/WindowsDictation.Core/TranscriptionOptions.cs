namespace WindowsDictation.Core;

public sealed record TranscriptionOptions(
    ModelKind Model,
    string Language,
    bool TranslateToEnglish = false)
{
    public static TranscriptionOptions FromSettings(AppSettings settings) => new(settings.SelectedModel, "en");
}

public sealed record TranscriptionResult(string Text, TimeSpan AudioDuration);
