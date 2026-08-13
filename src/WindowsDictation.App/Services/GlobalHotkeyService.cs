using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using WindowsDictation.Core;

namespace WindowsDictation.App.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x4454;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private readonly HwndSource source;
    private bool registered;
    private HotkeySettings? registeredSettings;

    public GlobalHotkeyService()
    {
        source = new HwndSource(new HwndSourceParameters("WindowsDictationHotkeySink")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0
        });
        source.AddHook(WndProc);
    }

    public event EventHandler? HotkeyPressed;

    public void Register(HotkeySettings settings)
    {
        HotkeySettings? previousSettings = registeredSettings;
        Unregister();

        if (!TryRegister(settings, out Win32Exception? exception))
        {
            if (previousSettings is not null)
            {
                TryRegister(previousSettings, out _);
            }

            throw exception ?? new Win32Exception("Unable to register the global dictation hotkey.");
        }
    }

    public void Unregister()
    {
        if (!registered)
        {
            return;
        }

        UnregisterHotKey(source.Handle, HotkeyId);
        registered = false;
        registeredSettings = null;
    }

    public void Dispose()
    {
        Unregister();
        source.RemoveHook(WndProc);
        source.Dispose();
    }

    private bool TryRegister(HotkeySettings settings, out Win32Exception? exception)
    {
        uint modifiers = ModNoRepeat;
        if (settings.Control)
        {
            modifiers |= ModControl;
        }

        if (settings.Alt)
        {
            modifiers |= ModAlt;
        }

        if (settings.Shift)
        {
            modifiers |= ModShift;
        }

        if (settings.Windows)
        {
            modifiers |= ModWin;
        }

        uint virtualKey = ResolveVirtualKey(settings.Key);
        if (!RegisterHotKey(source.Handle, HotkeyId, modifiers, virtualKey))
        {
            exception = new Win32Exception(Marshal.GetLastWin32Error(), "Unable to register the global dictation hotkey.");
            return false;
        }

        registered = true;
        registeredSettings = settings;
        exception = null;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    private static uint ResolveVirtualKey(string keyName)
    {
        if (Enum.TryParse(keyName, ignoreCase: true, out Key key))
        {
            return (uint)KeyInterop.VirtualKeyFromKey(key);
        }

        throw new ArgumentException($"Unsupported hotkey key '{keyName}'.", nameof(keyName));
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
