namespace WindowsDictation.Core;

public sealed class RecordingController : IRecordingController
{
    private readonly IAudioCaptureService audioCapture;
    private readonly ITranscriptionEngine transcriptionEngine;
    private readonly ITextInsertionService textInsertion;
    private readonly IAppSettingsProvider settingsProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private RecordingState state = RecordingState.Idle;

    public RecordingController(
        IAudioCaptureService audioCapture,
        ITranscriptionEngine transcriptionEngine,
        ITextInsertionService textInsertion,
        IAppSettingsProvider settingsProvider)
    {
        this.audioCapture = audioCapture;
        this.transcriptionEngine = transcriptionEngine;
        this.textInsertion = textInsertion;
        this.settingsProvider = settingsProvider;
    }

    public event EventHandler<RecordingStateChangedEventArgs>? StateChanged;

    public RecordingState State => state;

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
        state = nextState;
        StateChanged?.Invoke(this, new RecordingStateChangedEventArgs(nextState, message));
    }
}
