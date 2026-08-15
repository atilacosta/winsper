using System.IO;
using System.Text.Json;

namespace WindowsDictation.App.Services;

public sealed record TranscriptionHistoryEntry(DateTimeOffset CreatedAt, string Text);

public interface ITranscriptionHistoryStore
{
    Task<IReadOnlyList<TranscriptionHistoryEntry>> GetAllAsync(CancellationToken cancellationToken);
    Task AddAsync(string text, CancellationToken cancellationToken);
}

public sealed class TranscriptionHistoryStore : ITranscriptionHistoryStore
{
    private const int MaximumEntries = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly AppPaths paths;
    private readonly SemaphoreSlim gate = new(1, 1);

    public TranscriptionHistoryStore(AppPaths paths) => this.paths = paths;

    public async Task<IReadOnlyList<TranscriptionHistoryEntry>> GetAllAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try { return await ReadAsync(cancellationToken); }
        finally { gate.Release(); }
    }

    public async Task AddAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        await gate.WaitAsync(cancellationToken);
        try
        {
            List<TranscriptionHistoryEntry> entries = (await ReadAsync(cancellationToken)).ToList();
            entries.Insert(0, new TranscriptionHistoryEntry(DateTimeOffset.Now, text));
            string temporaryPath = paths.TranscriptionHistoryPath + ".tmp";
            await using (FileStream stream = File.Create(temporaryPath))
                await JsonSerializer.SerializeAsync(stream, entries.Take(MaximumEntries), JsonOptions, cancellationToken);
            File.Move(temporaryPath, paths.TranscriptionHistoryPath, overwrite: true);
        }
        finally { gate.Release(); }
    }

    private async Task<IReadOnlyList<TranscriptionHistoryEntry>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.TranscriptionHistoryPath)) return [];
        await using FileStream stream = File.OpenRead(paths.TranscriptionHistoryPath);
        return await JsonSerializer.DeserializeAsync<List<TranscriptionHistoryEntry>>(stream, JsonOptions, cancellationToken) ?? [];
    }
}
