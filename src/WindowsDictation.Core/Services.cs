namespace WindowsDictation.Core;

public interface IRecordingController
{
    event EventHandler<RecordingStateChangedEventArgs>? StateChanged;

    RecordingState State { get; }

    Task<RecordingResult> ToggleAsync(CancellationToken cancellationToken = default);
    Task<RecordingResult> StartAsync(CancellationToken cancellationToken = default);
    Task<RecordingResult> StopAndTranscribeAsync(CancellationToken cancellationToken = default);
    Task<RecordingResult> CancelAsync(CancellationToken cancellationToken = default);
}

public interface IAudioCaptureService
{
    Task StartAsync(CancellationToken cancellationToken);
    Task<AudioBuffer> StopAsync(CancellationToken cancellationToken);
    Task CancelAsync(CancellationToken cancellationToken);
}

public interface ITranscriptionEngine
{
    Task<TranscriptionResult> TranscribeAsync(
        AudioBuffer audio,
        TranscriptionOptions options,
        CancellationToken cancellationToken);
}

public interface ITextInsertionService
{
    Task<TextInsertionResult> InsertAsync(
        string text,
        InsertionOptions options,
        CancellationToken cancellationToken);
}

public interface IModelManager
{
    IReadOnlyList<ModelDescriptor> SupportedModels { get; }

    Task<ModelResolution> EnsureModelAsync(
        ModelKind model,
        IProgress<ModelDownloadProgress>? progress,
        CancellationToken cancellationToken);
}

public interface IAppSettingsProvider
{
    AppSettings Current { get; }
}

public interface ISettingsStore : IAppSettingsProvider
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}
