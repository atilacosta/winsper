using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using WindowsDictation.Core;

namespace WindowsDictation.App.Services;

public sealed class WindowsTextInsertionService : ITextInsertionService
{
    private const uint InputKeyboard = 1;
    private const ushort VkControl = 0x11;
    private const ushort VkShift = 0x10;
    private const ushort VkV = 0x56;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint KeyEventFUnicode = 0x0004;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const int TokenElevation = 20;
    private readonly PerformanceMetricsLogger metricsLogger;

    public WindowsTextInsertionService(PerformanceMetricsLogger metricsLogger)
    {
        this.metricsLogger = metricsLogger;
    }

    public async Task<TextInsertionResult> InsertAsync(
        string text,
        InsertionOptions options,
        CancellationToken cancellationToken)
    {
        IntPtr targetWindow = GetForegroundWindow();
        string targetProcess = GetProcessName(targetWindow);
        (bool clipboardUpdated, string? clipboardFailure) = await TrySetClipboardAsync(text);
        if (!clipboardUpdated)
        {
            return TextInsertionResult.Failure(clipboardFailure ?? "Unable to update the clipboard.", clipboardUpdated);
        }

        if (targetWindow != IntPtr.Zero)
        {
            SetForegroundWindow(targetWindow);
            await Task.Delay(30, cancellationToken).ConfigureAwait(false);
        }

        bool terminalPaste = IsWezTermWindow(targetWindow);
        bool elevatedTarget = IsForegroundProcessElevatedAboveCurrent();

        if (elevatedTarget)
        {
            metricsLogger.Log("insertion", TimeSpan.Zero,
                $"target={targetProcess};hwnd=0x{targetWindow.ToInt64():X};clipboard=true;elevated=true;result=blocked");
            return TextInsertionResult.Failure(
                "The active target is elevated. The transcript is on the clipboard, but Windows blocked paste/injection from this app.",
                clipboardUpdated);
        }

        bool inserted = options.Mode switch
        {
            InsertionMode.ClipboardPasteOnly => terminalPaste
                ? SendUnicodeText(text) || SendPasteShortcut(true)
                : SendPasteShortcut(false),
            InsertionMode.UnicodeOnly => SendUnicodeText(text),
            InsertionMode.ClipboardPasteThenUnicodeFallback => terminalPaste
                ? SendUnicodeText(text) || SendPasteShortcut(true)
                : SendPasteShortcut(false) || SendUnicodeText(text),
            _ => false
        };

        metricsLogger.Log("insertion", TimeSpan.Zero,
            $"target={targetProcess};hwnd=0x{targetWindow.ToInt64():X};clipboard=true;elevated=false;terminal={terminalPaste};result={inserted};sent={lastSentInputs};error={lastInputError}");

        return inserted
            ? TextInsertionResult.Success(clipboardUpdated)
            : TextInsertionResult.Failure("Windows did not accept paste or direct Unicode input for the active target.", clipboardUpdated);
    }

    private static async Task<(bool Updated, string? Failure)> TrySetClipboardAsync(string text)
    {
        if (System.Windows.Application.Current?.Dispatcher is not { } dispatcher || dispatcher.CheckAccess())
        {
            return TrySetClipboard(text);
        }

        return await dispatcher.InvokeAsync(() => TrySetClipboard(text));
    }

    private static (bool Updated, string? Failure) TrySetClipboard(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text, System.Windows.TextDataFormat.UnicodeText);
            return (true, null);
        }
        catch (Exception exception) when (exception is ExternalException or COMException)
        {
            return (false, exception.Message);
        }
    }

    private static bool SendPasteShortcut(bool terminalPaste)
    {
        INPUT[] inputs = terminalPaste
            ? [KeyDown(VkControl), KeyDown(VkShift), KeyDown(VkV), KeyUp(VkV), KeyUp(VkShift), KeyUp(VkControl)]
            : [KeyDown(VkControl), KeyDown(VkV), KeyUp(VkV), KeyUp(VkControl)];

        return SendInputs(inputs);
    }

    private static bool IsWezTermWindow(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(window, out uint processId);
        if (processId == 0)
        {
            return false;
        }

        try
        {
            using Process process = Process.GetProcessById((int)processId);
            return string.Equals(process.ProcessName, "wezterm-gui", StringComparison.OrdinalIgnoreCase)
                || string.Equals(process.ProcessName, "wezterm", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool SendUnicodeText(string text)
    {
        List<INPUT> inputs = new(text.Length * 2);
        foreach (char character in text)
        {
            inputs.Add(UnicodeKey(character, keyUp: false));
            inputs.Add(UnicodeKey(character, keyUp: true));
        }

        return SendInputs(inputs.ToArray());
    }

    private static bool SendInputs(INPUT[] inputs)
    {
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        lastSentInputs = sent;
        lastInputError = sent == inputs.Length ? 0 : Marshal.GetLastWin32Error();
        return sent == inputs.Length;
    }

    private static uint lastSentInputs;
    private static int lastInputError;

    private static string GetProcessName(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return "none";
        }

        GetWindowThreadProcessId(window, out uint processId);
        if (processId == 0)
        {
            return "unknown";
        }

        try
        {
            using Process process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return "unknown";
        }
    }

    private static INPUT KeyDown(ushort virtualKey)
    {
        return new INPUT
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KEYBDINPUT { VirtualKey = virtualKey }
            }
        };
    }

    private static INPUT KeyUp(ushort virtualKey)
    {
        INPUT input = KeyDown(virtualKey);
        input.Union.Keyboard.Flags = KeyEventFKeyUp;
        return input;
    }

    private static INPUT UnicodeKey(char character, bool keyUp)
    {
        return new INPUT
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KEYBDINPUT
                {
                    Scan = character,
                    Flags = KeyEventFUnicode | (keyUp ? KeyEventFKeyUp : 0)
                }
            }
        };
    }

    private static bool IsForegroundProcessElevatedAboveCurrent()
    {
        IntPtr foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(foregroundWindow, out uint processId);
        if (processId == 0)
        {
            return false;
        }

        using Process currentProcess = Process.GetCurrentProcess();
        bool currentElevated = IsProcessElevated(currentProcess.Handle);

        IntPtr targetProcess = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (targetProcess == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            bool targetElevated = IsProcessElevated(targetProcess);
            return targetElevated && !currentElevated;
        }
        finally
        {
            CloseHandle(targetProcess);
        }
    }

    private static bool IsProcessElevated(IntPtr processHandle)
    {
        if (!OpenProcessToken(processHandle, TokenQuery, out IntPtr tokenHandle))
        {
            return false;
        }

        try
        {
            TOKEN_ELEVATION elevation = default;
            int size = Marshal.SizeOf<TOKEN_ELEVATION>();
            if (!GetTokenInformation(tokenHandle, TokenElevation, ref elevation, size, out _))
            {
                return false;
            }

            return elevation.TokenIsElevated != 0;
        }
        finally
        {
            CloseHandle(tokenHandle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(
        IntPtr tokenHandle,
        int tokenInformationClass,
        ref TOKEN_ELEVATION tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort VirtualKey;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_ELEVATION
    {
        public int TokenIsElevated;
    }
}

