using System.IO;
using System.Text.Json;
using WindowsDictation.Core;

namespace WindowsDictation.App.Services;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly AppPaths paths;

    public JsonSettingsStore(AppPaths paths)
    {
        this.paths = paths;
    }

    public AppSettings Current { get; private set; } = new();

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.SettingsPath))
        {
            Current = new AppSettings();
            await SaveAsync(Current, cancellationToken);
            return Current;
        }

        await using FileStream stream = File.OpenRead(paths.SettingsPath);
        Current = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
            ?? new AppSettings();

        return Current;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        Current = settings;

        string tempPath = paths.SettingsPath + ".tmp";
        await using (FileStream stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
        }

        File.Move(tempPath, paths.SettingsPath, overwrite: true);
    }
}

