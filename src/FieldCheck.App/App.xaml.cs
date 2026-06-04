using System.IO;
using System.Windows;
using System.Windows.Threading;
using FieldCheck.App.Services;
using FieldCheck.App.ViewModels;
using FieldCheck.Core.Services;
using FieldCheck.Core.Utilities;

namespace FieldCheck.App;

public partial class App : Application
{
    /// <summary>The composed services, exposed so the window can reach the state service for geometry.</summary>
    public AppServices Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // All user data lives under %APPDATA%\FieldCheck.
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FieldCheck");

        var storage = new FileStorageService(dataDirectory);
        var stateService = new AppStateService(storage);
        var loadResult = stateService.Load();

        var theme = new ThemeService();
        var dialogs = new DialogService();
        Services = new AppServices(stateService, theme, dialogs, SystemClock.Instance, IdGenerator.Instance);

        // Apply the saved theme before any window is shown to avoid a flash.
        theme.Apply(stateService.State.Settings.Theme);

        // Safety net: contain any unforeseen UI-thread exception, flush pending data, and tell
        // the user instead of letting the process crash and lose a debounced save.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var notice = loadResult.Status is LoadStatus.RestoredFromBackup or LoadStatus.StartedEmptyCorrupt
            ? loadResult.Message
            : null;

        var viewModel = new MainViewModel(Services, notice);
        var window = new MainWindow(viewModel, stateService);
        dialogs.OwnerWindow = window;
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Make sure any debounced save is written before we quit.
        Services?.State.Flush();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        TryLogError(e.Exception);
        try
        {
            Services?.State.Flush();
            Services?.Dialogs.ShowMessage(
                "Unexpected error",
                $"FieldCheck hit an unexpected problem but kept your data:\n\n{e.Exception.Message}");
        }
        catch
        {
            // Never let the safety net itself bring down the app.
        }
        e.Handled = true;
    }

    /// <summary>Appends unexpected errors to %APPDATA%\FieldCheck\error.log for later troubleshooting.</summary>
    private static void TryLogError(Exception ex)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FieldCheck");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging is best-effort.
        }
    }
}
