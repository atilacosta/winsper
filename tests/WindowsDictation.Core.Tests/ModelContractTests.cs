using WindowsDictation.Core;

namespace WindowsDictation.Core.Tests;

public sealed class ModelContractTests
{
    [Fact]
    public async Task MissingModelTriggersDownloadAndPersistsPath()
    {
        FakeModelManager manager = new();

        ModelResolution resolution = await manager.EnsureModelAsync(ModelKind.BaseEnglish, null, CancellationToken.None);

        Assert.True(resolution.WasDownloaded);
        Assert.True(resolution.Path.EndsWith("ggml-base.en.bin", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExistingValidModelDoesNotDownload()
    {
        FakeModelManager manager = new(existingValidModel: true);

        ModelResolution resolution = await manager.EnsureModelAsync(ModelKind.TinyEnglish, null, CancellationToken.None);

        Assert.False(resolution.WasDownloaded);
        Assert.Equal(0, manager.DownloadCount);
    }

    [Fact]
    public async Task FailedValidationThrowsCleanly()
    {
        FakeModelManager manager = new(validationFails: true);

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => manager.EnsureModelAsync(ModelKind.SmallEnglish, null, CancellationToken.None));

        Assert.Contains("validation", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeModelManager : IModelManager
    {
        private readonly bool existingValidModel;
        private readonly bool validationFails;

        public FakeModelManager(bool existingValidModel = false, bool validationFails = false)
        {
            this.existingValidModel = existingValidModel;
            this.validationFails = validationFails;
        }

        public int DownloadCount { get; private set; }

        public IReadOnlyList<ModelDescriptor> SupportedModels { get; } =
        [
            new(ModelKind.TinyEnglish, "Tiny English", "ggml-tiny.en.bin", 1),
            new(ModelKind.BaseEnglish, "Base English", "ggml-base.en.bin", 1),
            new(ModelKind.SmallEnglish, "Small English", "ggml-small.en.bin", 1)
        ];

        public Task<ModelResolution> EnsureModelAsync(
            ModelKind model,
            IProgress<ModelDownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            ModelDescriptor descriptor = SupportedModels.Single(candidate => candidate.Kind == model);
            string path = Path.Combine("models", descriptor.FileName);

            if (existingValidModel)
            {
                return Task.FromResult(new ModelResolution(path, WasDownloaded: false));
            }

            DownloadCount++;
            if (validationFails)
            {
                throw new InvalidDataException($"Downloaded model '{descriptor.FileName}' failed validation.");
            }

            return Task.FromResult(new ModelResolution(path, WasDownloaded: true));
        }
    }
}
