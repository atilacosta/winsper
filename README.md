# Windows Dictation

A Windows 11+ local dictation utility inspired by the OpenSuperWhisper recording flow. Press `Ctrl+Space` to start recording, press it again to stop, transcribe locally with Whisper, copy the transcript to the clipboard, and paste it into the active target.

## Project Layout

- `src/WindowsDictation.Core`: testable recording workflow, state machine, settings, model, transcription, and insertion contracts.
- `src/WindowsDictation.App`: WPF tray app, top-center overlay, global hotkey, NAudio microphone capture, Whisper.net transcription, clipboard/paste insertion, and startup registration.
- `tests/WindowsDictation.Core.Tests`: focused unit tests for the recording state machine and model-management contract behavior.

## Requirements

- Windows 11 or Windows Server 2022 or newer.
- .NET 10 SDK.
- Microsoft Visual C++ Redistributable for Visual Studio 2022 x64.
- A CPU compatible with the default `Whisper.net.Runtime` package. If a target machine lacks AVX support, swap the runtime package to `Whisper.net.Runtime.NoAvx`.

## Build And Run

```powershell
dotnet restore .\WindowsDictation.sln
dotnet build .\WindowsDictation.sln -c Release
dotnet run --project .\src\WindowsDictation.App\WindowsDictation.App.csproj
```

## Publish And Package

```powershell
.\build\publish.ps1
iscc .\installer\WindowsDictation.iss
```

The installer definition is configured for per-user installation. To produce a signed installer, configure a SignTool entry in Inno Setup and uncomment the signing lines in `installer\WindowsDictation.iss`.

The first transcription downloads the selected GGML model into `%LOCALAPPDATA%\WindowsDictation\Models`. The default model is `ggml-base.en.bin`; `tiny.en` and `small.en` can be selected in settings.

## Behavior

- Default hotkey: `Ctrl+Space`.
- Overlay: fixed top-center indicator for ready, recording, transcribing, pasting, and error states.
- Insertion: always writes the final transcript to the clipboard, then uses paste or Unicode input based on settings.
- Elevated targets: if the target app is elevated and Windows Dictation is not, insertion is reported as blocked and the transcript remains on the clipboard.
- Startup: the setting writes only to the current user's `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` key.

## Tests

```powershell
dotnet test .\WindowsDictation.sln
```

Manual acceptance checks:

- `Ctrl+Space` starts and stops recording globally.
- Overlay state changes for recording, transcribing, pasted, and error.
- Transcript pastes into Notepad, a browser text field, VS Code, Windows Terminal, and an Office-style editor.
- Clipboard contains the transcript after paste.
- An elevated target app reports a blocked insertion without losing the transcript.
- Exiting the app stops any active recording.
