namespace WindowsDictation.Core;

public sealed record InsertionOptions(InsertionMode Mode)
{
    public static InsertionOptions FromSettings(AppSettings settings) => new(settings.InsertionMode);
}

public enum InsertionMode
{
    ClipboardPasteThenUnicodeFallback,
    ClipboardPasteOnly,
    UnicodeOnly
}

public sealed record TextInsertionResult(
    bool Inserted,
    bool ClipboardUpdated,
    string? FailureReason)
{
    public static TextInsertionResult Success(bool clipboardUpdated = true) => new(true, clipboardUpdated, null);
    public static TextInsertionResult Failure(string reason, bool clipboardUpdated) => new(false, clipboardUpdated, reason);
}
