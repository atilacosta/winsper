namespace WindowsDictation.Core;

public sealed record AudioBuffer(
    byte[] WavBytes,
    int SampleRate,
    int ChannelCount,
    TimeSpan Duration)
{
    public bool HasAudio => WavBytes.Length > 44 && Duration > TimeSpan.Zero;
}
