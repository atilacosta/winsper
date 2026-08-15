using System.IO;

namespace WindowsDictation.App.Services;

public sealed class AppPaths
{
    public AppPaths()
    {
        RootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsDictation");
        ModelsDirectory = Path.Combine(RootDirectory, "Models");
        SettingsPath = Path.Combine(RootDirectory, "settings.json");
        TranscriptionHistoryPath = Path.Combine(RootDirectory, "transcription-history.json");

        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(ModelsDirectory);
    }

    public string RootDirectory { get; }
    public string ModelsDirectory { get; }
    public string SettingsPath { get; }
    public string TranscriptionHistoryPath { get; }
}

