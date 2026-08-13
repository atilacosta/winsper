namespace WindowsDictation.Core;

public enum ModelKind
{
    TinyEnglish,
    BaseEnglish,
    SmallEnglish
}

public sealed record ModelDescriptor(
    ModelKind Kind,
    string DisplayName,
    string FileName,
    long MinimumBytes);

public sealed record ModelResolution(string Path, bool WasDownloaded);

public sealed record ModelDownloadProgress(ModelKind Model, long? BytesReceived, long? TotalBytes);
