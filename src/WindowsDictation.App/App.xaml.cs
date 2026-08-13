using System.ComponentModel;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WindowsDictation.App.Services;
using WindowsDictation.App.Windows;
using WindowsDictation.Core;
using Forms = System.Windows.Forms;

namespace WindowsDictation.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? serviceProvider;
    private Forms.NotifyIcon? notifyIcon;
    private GlobalHotkeyService? hotkeyService;
    private OverlayWindow? overlayWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ServiceCollection services = new();
        ConfigureServices(services);
        serviceProvider = services.BuildServiceProvider();

        ISettingsStore settingsStore = serviceProvider.GetRequiredService<ISettingsStore>();
        await settingsStore.LoadAsync(CancellationToken.None);

        _ = Task.Run(async () =>
        {
            try
            {
                await serviceProvider.GetRequiredService<WhisperNetTranscriptionEngine>()
                    .WarmupAsync(settingsStore.Current.SelectedModel, CancellationToken.None);
            }
            catch
            {
                // The first transcription retries initialization and reports any failure in the overlay.
            }
        });

        overlayWindow = serviceProvider.GetRequiredService<OverlayWindow>();
        overlayWindow.ShowState(RecordingState.Idle, "Ready");

        IRecordingController controller = serviceProvider.GetRequiredService<IRecordingController>();
        controller.StateChanged += (_, args) => Dispatcher.Invoke(() => overlayWindow.ShowState(args.State, args.Message));

        hotkeyService = serviceProvider.GetRequiredService<GlobalHotkeyService>();
        hotkeyService.HotkeyPressed += async (_, _) => await controller.ToggleAsync();
        try
        {
            hotkeyService.Register(settingsStore.Current.Hotkey);
        }
        catch (Win32Exception exception)
        {
            overlayWindow.ShowState(RecordingState.Error, exception.Message);
        }

        notifyIcon = CreateNotifyIcon();
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        hotkeyService?.Dispose();
        notifyIcon?.Dispose();

        if (serviceProvider?.GetService<IRecordingController>() is IRecordingController controller)
        {
            controller.CancelAsync().GetAwaiter().GetResult();
        }

        serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<AppPaths>();
        services.AddSingleton<PerformanceMetricsLogger>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IAppSettingsProvider>(sp => sp.GetRequiredService<ISettingsStore>());
        services.AddSingleton<IAudioCaptureService, WaveInAudioCaptureService>();
        services.AddSingleton<IModelManager, WhisperNetModelManager>();
        services.AddSingleton<ITranscriptionEngine, WhisperNetTranscriptionEngine>();
        services.AddSingleton<ITextInsertionService, WindowsTextInsertionService>();
        services.AddSingleton<IRecordingController, RecordingController>();
        services.AddSingleton<AudioDeviceCatalog>();
        services.AddSingleton<StartupRegistrationService>();
        services.AddSingleton<GlobalHotkeyService>();
        services.AddTransient<SettingsWindow>();
        services.AddSingleton<OverlayWindow>();
    }

    private Forms.NotifyIcon CreateNotifyIcon()
    {
        Forms.ContextMenuStrip menu = new();
        menu.Items.Add("Settings", null, (_, _) => Dispatcher.Invoke(ShowSettings));
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(Shutdown));

        Forms.NotifyIcon icon = new()
        {
            Text = "Windows Dictation",
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };

        icon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowSettings);
        return icon;
    }

    private void ShowSettings()
    {
        SettingsWindow window = serviceProvider!.GetRequiredService<SettingsWindow>();
        window.Owner = overlayWindow;
        window.Show();
        window.Activate();
    }
}


