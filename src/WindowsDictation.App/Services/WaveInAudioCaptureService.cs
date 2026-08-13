using System.Diagnostics;
using System.IO;
using NAudio.Wave;
using WindowsDictation.Core;

namespace WindowsDictation.App.Services;

public sealed class WaveInAudioCaptureService : IAudioCaptureService, IDisposable
{
    private static readonly WaveFormat CaptureFormat = new(16_000, 16, 1);

    private readonly IAppSettingsProvider settingsProvider;
    private readonly object sync = new();

    private WaveInEvent? waveIn;
    private MemoryStream? rawPcm;
    private Stopwatch? stopwatch;
    private TaskCompletionSource? stopped;

    public WaveInAudioCaptureService(IAppSettingsProvider settingsProvider)
    {
        this.settingsProvider = settingsProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (sync)
        {
            if (waveIn is not null)
            {
                throw new InvalidOperationException("Recording is already active.");
            }

            rawPcm = new MemoryStream();
            stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            stopwatch = Stopwatch.StartNew();

            try
            {
                waveIn = new WaveInEvent
                {
                    DeviceNumber = ResolveDeviceNumber(settingsProvider.Current.MicrophoneDeviceId),
                    WaveFormat = CaptureFormat,
                    BufferMilliseconds = 50
                };
                waveIn.DataAvailable += OnDataAvailable;
                waveIn.RecordingStopped += OnRecordingStopped;
                waveIn.StartRecording();
            }
            catch
            {
                DisposeRecorder();
                rawPcm?.Dispose();
                rawPcm = null;
                stopwatch = null;
                stopped = null;
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public async Task<AudioBuffer> StopAsync(CancellationToken cancellationToken)
    {
        WaveInEvent currentWaveIn;
        TaskCompletionSource currentStopped;

        lock (sync)
        {
            currentWaveIn = waveIn ?? throw new InvalidOperationException("Recording is not active.");
            currentStopped = stopped ?? throw new InvalidOperationException("Recording is not active.");
            currentWaveIn.StopRecording();
        }

        await currentStopped.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

        lock (sync)
        {
            byte[] wavBytes = CreateWavBytes(rawPcm?.ToArray() ?? []);
            TimeSpan duration = stopwatch?.Elapsed ?? TimeSpan.Zero;

            DisposeRecorder();
            rawPcm?.Dispose();
            rawPcm = null;
            stopwatch = null;
            stopped = null;

            return new AudioBuffer(wavBytes, CaptureFormat.SampleRate, CaptureFormat.Channels, duration);
        }
    }

    public Task CancelAsync(CancellationToken cancellationToken)
    {
        lock (sync)
        {
            if (waveIn is not null)
            {
                waveIn.StopRecording();
            }

            DisposeRecorder();
            rawPcm?.Dispose();
            rawPcm = null;
            stopwatch = null;
            stopped?.TrySetResult();
            stopped = null;
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (sync)
        {
            DisposeRecorder();
            rawPcm?.Dispose();
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (sync)
        {
            rawPcm?.Write(e.Buffer, 0, e.BytesRecorded);
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            stopped?.TrySetException(e.Exception);
            return;
        }

        stopped?.TrySetResult();
    }

    private static byte[] CreateWavBytes(byte[] rawBytes)
    {
        using MemoryStream wavStream = new();
        using (WaveFileWriter writer = new(wavStream, CaptureFormat))
        {
            writer.Write(rawBytes, 0, rawBytes.Length);
        }

        return wavStream.ToArray();
    }

    private static int ResolveDeviceNumber(string? microphoneDeviceId)
    {
        if (int.TryParse(microphoneDeviceId, out int deviceNumber)
            && deviceNumber >= 0
            && deviceNumber < WaveIn.DeviceCount)
        {
            return deviceNumber;
        }

        return 0;
    }

    private void DisposeRecorder()
    {
        if (waveIn is null)
        {
            return;
        }

        waveIn.DataAvailable -= OnDataAvailable;
        waveIn.RecordingStopped -= OnRecordingStopped;
        waveIn.Dispose();
        waveIn = null;
    }
}


