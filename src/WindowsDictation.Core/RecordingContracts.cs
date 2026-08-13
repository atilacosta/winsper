namespace WindowsDictation.Core;

public enum RecordingState
{
    Idle,
    Recording,
    Transcribing,
    Inserting,
    Error
}

public enum RecordingAction
{
    Started,
    Completed,
    Canceled,
    Ignored,
    Failed
}

public sealed record RecordingResult(
    RecordingAction Action,
    RecordingState State,
    string? Transcript = null,
    string? Error = null)
{
    public bool Success => Action is RecordingAction.Started or RecordingAction.Completed or RecordingAction.Canceled;
}

public sealed class RecordingStateChangedEventArgs : EventArgs
{
    public RecordingStateChangedEventArgs(RecordingState state, string? message = null)
    {
        State = state;
        Message = message;
    }

    public RecordingState State { get; }
    public string? Message { get; }
}
