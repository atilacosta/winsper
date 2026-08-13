using Microsoft.Win32;

namespace WindowsDictation.App.Services;

public sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WindowsDictation";

    public void Apply(bool enabled)
    {
        using RegistryKey runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Cannot open the current-user startup registry key.");

        if (enabled)
        {
            string executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Cannot resolve the current executable path.");
            runKey.SetValue(ValueName, Quote(executablePath), RegistryValueKind.String);
            return;
        }

        runKey.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string Quote(string value)
    {
        return value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
    }
}
