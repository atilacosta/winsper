using System.Diagnostics;
using System.IO;
using System.Text;
using Whisper.net;
using WindowsDictation.Core;

namespace WindowsDictation.App.Services;

public sealed class WhisperNetTranscriptionEngine : ITranscriptionEngine, IDisposable
{
    private readonly IModelManager modelManager;
    private readonly PerformanceMetricsLogger metricsLogger;
    private readonly SemaphoreSlim initializationGate = new(1, 1);
    private WhisperFactory? whisperFactory;
    private WhisperProcessor? processor;
    private string? loadedModelPath;

    public WhisperNetTranscriptionEngine(IModelManager modelManager, PerformanceMetricsLogger metricsLogger)
    {
        this.modelManager = modelManager;
        this.metricsLogger = metricsLogger;
    }

    public async Task WarmupAsync(ModelKind model, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        WhisperProcessor activeProcessor = await EnsureProcessorAsync(model, cancellationToken).ConfigureAwait(false);
        await using MemoryStream audioStream = new(CreateSilenceWav());
        await foreach (var _ in activeProcessor.ProcessAsync(audioStream, cancellationToken).ConfigureAwait(false))
        {
        }

        metricsLogger.Log("warmup", stopwatch.Elapsed, $"model={model}");
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        AudioBuffer audio,
        TranscriptionOptions options,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        WhisperProcessor activeProcessor = await EnsureProcessorAsync(options.Model, cancellationToken).ConfigureAwait(false);

        await using MemoryStream audioStream = new(audio.WavBytes);
        StringBuilder transcript = new();

        await foreach (var segment in activeProcessor.ProcessAsync(audioStream, cancellationToken).ConfigureAwait(false))
        {
            transcript.Append(segment.Text);
        }

        metricsLogger.Log("transcription", stopwatch.Elapsed,
            $"model={options.Model};audio={audio.Duration.TotalSeconds:F2}s;realtime_factor={audio.Duration.TotalSeconds / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001):F2}");
        return new TranscriptionResult(transcript.ToString(), audio.Duration);
    }

    public void Dispose()
    {
        processor?.Dispose();
        whisperFactory?.Dispose();
        initializationGate.Dispose();
    }

    private async Task<WhisperProcessor> EnsureProcessorAsync(ModelKind model, CancellationToken cancellationToken)
    {
        try
        {
            return await EnsureProcessorCoreAsync(model, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Speech model initialization failed.", exception);
        }
    }

    private async Task<WhisperProcessor> EnsureProcessorCoreAsync(ModelKind model, CancellationToken cancellationToken)
    {
        ModelResolution resolution = await modelManager
            .EnsureModelAsync(model, progress: null, cancellationToken)
            .ConfigureAwait(false);

        if (processor is not null && loadedModelPath == resolution.Path)
        {
            return processor;
        }

        await initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (processor is not null && loadedModelPath == resolution.Path)
            {
                return processor;
            }

            processor?.Dispose();
            whisperFactory?.Dispose();
            whisperFactory = WhisperFactory.FromPath(resolution.Path);
            processor = whisperFactory.CreateBuilder()
                .WithLanguage("en")
                .WithThreads(Math.Max(1, Environment.ProcessorCount))
                .Build();
            loadedModelPath = resolution.Path;
            return processor;
        }
        finally
        {
            initializationGate.Release();
        }
    }

    private static byte[] CreateSilenceWav()
    {
        using MemoryStream stream = new();
        using (var writer = new NAudio.Wave.WaveFileWriter(stream, new NAudio.Wave.WaveFormat(16_000, 16, 1)))
        {
            writer.Write(new byte[16_000], 0, 16_000);
        }

        return stream.ToArray();
    }
}
