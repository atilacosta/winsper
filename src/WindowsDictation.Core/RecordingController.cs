namespace WindowsDictation.Core;

public sealed class RecordingController : IRecordingController
{
    private static readonly TimeSpan DefaultErrorDisplayDuration = TimeSpan.FromSeconds(5);

    private readonly IAudioCaptureService audioCapture;
    private readonly ITranscriptionEngine transcriptionEngine;
    private readonly ITextInsertionService textInsertion;
    private readonly IAppSettingsProvider settingsProvider;
    private readonly TimeSpan errorDisplayDuration;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object stateLock = new();

    private RecordingState state = RecordingState.Idle;
    private CancellationTokenSource? errorClearCancellation;

    public RecordingController(
        IAudioCaptureService audioCapture,
        ITranscriptionEngine transcriptionEngine,
        ITextInsertionService textInsertion,
        IAppSettingsProvider settingsProvider,
        TimeSpan? errorDisplayDuration = null)
    {
        this.audioCapture = audioCapture;
        this.transcriptionEngine = transcriptionEngine;
        this.textInsertion = textInsertion;
        this.settingsProvider = settingsProvider;
        this.errorDisplayDuration = errorDisplayDuration ?? DefaultErrorDisplayDuration;
        if (this.errorDisplayDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(errorDisplayDuration),
                "The error display duration must be greater than zero.");
        }
    }

    public event EventHandler<RecordingStateChangedEventArgs>? StateChanged;

    public RecordingState State
    {
        get
        {
            lock (stateLock)
            {
                return state;
            }
        }
    }

    public Task<RecordingResult> ToggleAsync(CancellationToken cancellationToken = default)
    {
        return State == RecordingState.Recording
            ? StopAndTranscribeAsync(cancellationToken)
            : StartAsync(cancellationToken);
    }

    public async Task<RecordingResult> StartAsync(CancellationToken cancellationToken = default)
    {
        if (!await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new RecordingResult(RecordingAction.Ignored, state);
        }

        try
        {
            if (state is RecordingState.Recording or RecordingState.Transcribing or RecordingState.Inserting)
            {
                return new RecordingResult(RecordingAction.Ignored, state);
            }

            SetState(RecordingState.Recording, "Recording");
            await audioCapture.StartAsync(cancellationToken).ConfigureAwait(false);

            return new RecordingResult(RecordingAction.Started, state);
        }
        catch (Exception exception)
        {
            SetState(RecordingState.Error, exception.Message);
            return new RecordingResult(RecordingAction.Failed, state, Error: exception.Message);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<RecordingResult> StopAndTranscribeAsync(CancellationToken cancellationToken = default)
    {
        if (!await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new RecordingResult(RecordingAction.Ignored, state);
        }

        try
        {
            if (state != RecordingState.Recording)
            {
                return new RecordingResult(RecordingAction.Ignored, state);
            }

            AudioBuffer audio = await audioCapture.StopAsync(cancellationToken).ConfigureAwait(false);
            if (!audio.HasAudio)
            {
                SetState(RecordingState.Idle, "No audio captured");
                return new RecordingResult(RecordingAction.Completed, state, Transcript: string.Empty);
            }

            SetState(RecordingState.Transcribing, "Transcribing");
            TranscriptionResult transcription = await transcriptionEngine
                .TranscribeAsync(audio, TranscriptionOptions.FromSettings(settingsProvider.Current), cancellationToken)
                .ConfigureAwait(false);

            string transcript = transcription.Text.Trim();
            if (transcript.Length == 0)
            {
                SetState(RecordingState.Idle, "No speech detected");
                return new RecordingResult(RecordingAction.Completed, state, Transcript: string.Empty);
            }

            SetState(RecordingState.Inserting, "Inserting");
            TextInsertionResult insertion = await textInsertion
                .InsertAsync(transcript, InsertionOptions.FromSettings(settingsProvider.Current), cancellationToken)
                .ConfigureAwait(false);

            if (!insertion.Inserted)
            {
                string reason = insertion.FailureReason ?? "Text insertion failed";
                SetState(RecordingState.Error, reason);
                return new RecordingResult(RecordingAction.Failed, state, Transcript: transcript, Error: reason);
            }

            SetState(RecordingState.Idle, "Inserted");
            return new RecordingResult(RecordingAction.Completed, state, Transcript: transcript);
        }
        catch (Exception exception)
        {
            SetState(RecordingState.Error, exception.Message);
            return new RecordingResult(RecordingAction.Failed, state, Error: exception.Message);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<RecordingResult> CancelAsync(CancellationToken cancellationToken = default)
    {
        if (!await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new RecordingResult(RecordingAction.Ignored, state);
        }

        try
        {
            if (state == RecordingState.Recording)
            {
                await audioCapture.CancelAsync(cancellationToken).ConfigureAwait(false);
            }

            SetState(RecordingState.Idle, "Canceled");
            return new RecordingResult(RecordingAction.Canceled, state);
        }
        finally
        {
            gate.Release();
        }
    }

    private void SetState(RecordingState nextState, string? message = null)
    {
        CancellationTokenSource? previousErrorClear;
        CancellationTokenSource? nextErrorClear = null;
        CancellationToken nextErrorClearToken = default;

        lock (stateLock)
        {
            previousErrorClear = errorClearCancellation;
            errorClearCancellation = null;
            state = nextState;

            if (nextState == RecordingState.Error)
            {
                nextErrorClear = new CancellationTokenSource();
                errorClearCancellation = nextErrorClear;
                nextErrorClearToken = nextErrorClear.Token;
            }
        }

        previousErrorClear?.Cancel();
        previousErrorClear?.Dispose();
        StateChanged?.Invoke(this, new RecordingStateChangedEventArgs(nextState, message));

        if (nextErrorClear is not null)
        {
            _ = ClearErrorAfterDelayAsync(nextErrorClear, nextErrorClearToken);
        }
    }

    private async Task ClearErrorAfterDelayAsync(
        CancellationTokenSource cancellation,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(errorDisplayDuration, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (stateLock)
        {
            if (errorClearCancellation != cancellation || state != RecordingState.Error)
            {
                return;
            }

            errorClearCancellation = null;
            state = RecordingState.Idle;
        }

        cancellation.Dispose();
        StateChanged?.Invoke(this, new RecordingStateChangedEventArgs(RecordingState.Idle, "Ready"));
    }
}
