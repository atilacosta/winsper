using NAudio.Wave;

namespace WindowsDictation.App.Services;

public sealed class AudioDeviceCatalog
{
    public IReadOnlyList<AudioInputDevice> GetInputDevices()
    {
        List<AudioInputDevice> devices = [];
        for (int index = 0; index < WaveIn.DeviceCount; index++)
        {
            WaveInCapabilities capabilities = WaveIn.GetCapabilities(index);
            devices.Add(new AudioInputDevice(index.ToString(), capabilities.ProductName));
        }

        return devices;
    }
}

public sealed record AudioInputDevice(string Id, string Name);
