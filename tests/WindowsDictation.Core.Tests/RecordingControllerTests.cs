using WindowsDictation.Core;

namespace WindowsDictation.Core.Tests;

public sealed class RecordingControllerTests
{
    [Fact]
    public async Task StartAsync_FromIdle_StartsCaptureAndMovesToRecording()
    {
        FakeAudioCapture audio = new();
        RecordingController controller = CreateController(audio);

        RecordingResult result = await controller.StartAsync();

        Assert.Equal(RecordingAction.Started, result.Action);
        Assert.Equal(RecordingState.Recording, controller.State);
        Assert.Equal(1, audio.StartCount);
    }

    [Fact]
    public async Task StopAndTranscribeAsync_FromRecording_InsertsTranscriptAndReturnsIdle()
    {
        FakeAudioCapture audio = new();
        FakeTranscriptionEngine transcription = new("hello world");
        FakeTextInsertion insertion = new();
        RecordingController controller = CreateController(audio, transcription, insertion);

        await controller.StartAsync();
        RecordingResult result = await controller.StopAndTranscribeAsync();

        Assert.Equal(RecordingAction.Completed, result.Action);
        Assert.Equal("hello world", result.Transcript);
        Assert.Equal(RecordingState.Idle, controller.State);
        Assert.Equal("hello world", insertion.InsertedText);
    }

    [Fact]
    public async Task ToggleAsync_DuringTranscription_IsIgnored()
    {
        FakeAudioCapture audio = new();
        GateableTranscriptionEngine transcription = new();
        RecordingController controller = CreateController(audio, transcription);

        await controller.StartAsync();
        Task<RecordingResult> stopTask = controller.StopAndTranscribeAsync();

        await transcription.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        RecordingResult duplicate = await controller.ToggleAsync();

        Assert.Equal(RecordingAction.Ignored, duplicate.Action);
        Assert.Equal(RecordingState.Transcribing, duplicate.State);

        transcription.Release("done");
        RecordingResult completed = await stopTask;
        Assert.Equal(RecordingAction.Completed, completed.Action);
    }

    [Fact]
    public async Task StopAndTranscribeAsync_WhenTranscriptionFails_MovesToErrorAndCanRecover()
    {
        FakeAudioCapture audio = new();
        ThrowingTranscriptionEngine transcription = new();
        RecordingController controller = CreateController(audio, transcription);

        await controller.StartAsync();
        RecordingResult failed = await controller.StopAndTranscribeAsync();

        Assert.Equal(RecordingAction.Failed, failed.Action);
        Assert.Equal(RecordingState.Error, controller.State);

        transcription.Throw = false;
        RecordingResult restarted = await controller.StartAsync();

        Assert.Equal(RecordingAction.Started, restarted.Action);
        Assert.Equal(RecordingState.Recording, controller.State);
    }

    [Fact]
    public async Task CancelAsync_WhileRecording_StopsCaptureAndReturnsIdle()
    {
        FakeAudioCapture audio = new();
        RecordingController controller = CreateController(audio);

        await controller.StartAsync();
        RecordingResult canceled = await controller.CancelAsync();

        Assert.Equal(RecordingAction.Canceled, canceled.Action);
        Assert.Equal(RecordingState.Idle, controller.State);
        Assert.Equal(1, audio.CancelCount);
    }

    private static RecordingController CreateController(
        IAudioCaptureService? audio = null,
        ITranscriptionEngine? transcription = null,
        ITextInsertionService? insertion = null,
        IAppSettingsProvider? settings = null)
    {
        return new RecordingController(
            audio ?? new FakeAudioCapture(),
            transcription ?? new FakeTranscriptionEngine("transcript"),
            insertion ?? new FakeTextInsertion(),
            settings ?? new FakeSettingsProvider());
    }

    private sealed class FakeAudioCapture : IAudioCaptureService
    {
        public int StartCount { get; private set; }
        public int CancelCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCount++;
            return Task.CompletedTask;
        }

        public Task<AudioBuffer> StopAsync(CancellationToken cancellationToken)
        {
            byte[] wavBytes = new byte[64];
            return Task.FromResult(new AudioBuffer(wavBytes, 16_000, 1, TimeSpan.FromSeconds(1)));
        }

        public Task CancelAsync(CancellationToken cancellationToken)
        {
            CancelCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTranscriptionEngine : ITranscriptionEngine
    {
        private readonly string transcript;

        public FakeTranscriptionEngine(string transcript)
        {
            this.transcript = transcript;
        }

        public Task<TranscriptionResult> TranscribeAsync(
            AudioBuffer audio,
            TranscriptionOptions options,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new TranscriptionResult(transcript, audio.Duration));
        }
    }

    private sealed class GateableTranscriptionEngine : ITranscriptionEngine
    {
        private readonly TaskCompletionSource<string> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<TranscriptionResult> TranscribeAsync(
            AudioBuffer audio,
            TranscriptionOptions options,
            CancellationToken cancellationToken)
        {
            Started.SetResult();
            string transcript = await release.Task.WaitAsync(cancellationToken);
            return new TranscriptionResult(transcript, audio.Duration);
        }

        public void Release(string transcript)
        {
            release.SetResult(transcript);
        }
    }

    private sealed class ThrowingTranscriptionEngine : ITranscriptionEngine
    {
        public bool Throw { get; set; } = true;

        public Task<TranscriptionResult> TranscribeAsync(
            AudioBuffer audio,
            TranscriptionOptions options,
            CancellationToken cancellationToken)
        {
            return Throw
                ? Task.FromException<TranscriptionResult>(new InvalidOperationException("transcription failed"))
                : Task.FromResult(new TranscriptionResult("recovered", audio.Duration));
        }
    }

    private sealed class FakeTextInsertion : ITextInsertionService
    {
        public string? InsertedText { get; private set; }

        public Task<TextInsertionResult> InsertAsync(
            string text,
            InsertionOptions options,
            CancellationToken cancellationToken)
        {
            InsertedText = text;
            return Task.FromResult(TextInsertionResult.Success());
        }
    }

    private sealed class FakeSettingsProvider : IAppSettingsProvider
    {
        public AppSettings Current { get; } = new();
    }
}
