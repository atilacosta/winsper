using System.IO;
using Whisper.net.Ggml;
using WindowsDictation.Core;

namespace WindowsDictation.App.Services;

public sealed class WhisperNetModelManager : IModelManager
{
    private static readonly IReadOnlyDictionary<ModelKind, ModelDescriptor> Models = new Dictionary<ModelKind, ModelDescriptor>
    {
        [ModelKind.TinyEnglish] = new(ModelKind.TinyEnglish, "Tiny English - fastest", "ggml-tiny.en.bin", 10L * 1024 * 1024),
        [ModelKind.BaseEnglish] = new(ModelKind.BaseEnglish, "Base English - balanced", "ggml-base.en.bin", 50L * 1024 * 1024),
        [ModelKind.SmallEnglish] = new(ModelKind.SmallEnglish, "Small English - better quality", "ggml-small.en.bin", 100L * 1024 * 1024)
    };

    private readonly AppPaths paths;

    public WhisperNetModelManager(AppPaths paths)
    {
        this.paths = paths;
    }

    public IReadOnlyList<ModelDescriptor> SupportedModels => Models.Values.ToArray();

    public async Task<ModelResolution> EnsureModelAsync(
        ModelKind model,
        IProgress<ModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        ModelDescriptor descriptor = Models[model];
        string modelPath = Path.Combine(paths.ModelsDirectory, descriptor.FileName);

        if (IsValidModelFile(modelPath, descriptor))
        {
            return new ModelResolution(modelPath, WasDownloaded: false);
        }

        progress?.Report(new ModelDownloadProgress(model, null, null));

        string tempPath = modelPath + ".download";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        await using (Stream modelStream = await WhisperGgmlDownloader.Default
            .GetGgmlModelAsync(ToGgmlType(model))
            .ConfigureAwait(false))
        await using (FileStream fileStream = File.Create(tempPath))
        {
            await modelStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }

        if (!IsValidModelFile(tempPath, descriptor))
        {
            File.Delete(tempPath);
            throw new InvalidDataException($"Downloaded model '{descriptor.FileName}' failed size validation.");
        }

        File.Move(tempPath, modelPath, overwrite: true);
        return new ModelResolution(modelPath, WasDownloaded: true);
    }

    private static bool IsValidModelFile(string path, ModelDescriptor descriptor)
    {
        return File.Exists(path) && new FileInfo(path).Length >= descriptor.MinimumBytes;
    }

    private static GgmlType ToGgmlType(ModelKind model)
    {
        return model switch
        {
            ModelKind.TinyEnglish => GgmlType.TinyEn,
            ModelKind.BaseEnglish => GgmlType.BaseEn,
            ModelKind.SmallEnglish => GgmlType.SmallEn,
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, null)
        };
    }
}

