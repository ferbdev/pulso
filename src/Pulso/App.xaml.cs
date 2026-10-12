using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Pulso.Services;
using Pulso.ViewModels;
using Pulso.Views;

namespace Pulso;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\Pulso.SingleInstance";
    private const string WaitForPreviousArg = "--wait-for-previous";

    private Mutex? _singleInstanceMutex;
    private HardwareMonitorService? _monitor;
    private WidgetViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

#if DEBUG
        if (TryRunSnapshot(e.Args))
        {
            return;
        }
#endif

        if (!AcquireSingleInstance(waitForPrevious: e.Args.Contains(WaitForPreviousArg)))
        {
            Shutdown();
            return;
        }

#if !DEBUG
        // Só o build distribuído se registra, para o pulso:// não apontar para a pasta bin\Debug.
        ProtocolRegistration.EnsureRegistered();
#endif

        var settingsService = new SettingsService();
        var settings = settingsService.Load();

        _monitor = new HardwareMonitorService();
        _viewModel = new WidgetViewModel(_monitor, settingsService, settings)
        {
            RestartElevatedRequested = RestartElevated,
        };

        var window = new WidgetWindow(_viewModel, settings);
        MainWindow = window;
        window.Show();

        _viewModel.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.Dispose();
        _monitor?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private bool AcquireSingleInstance(bool waitForPrevious)
    {
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var isFirstInstance);
        if (isFirstInstance || !waitForPrevious)
        {
            return isFirstInstance;
        }

        // Reinício elevado: espera a instância anterior terminar de fechar.
        try
        {
            return _singleInstanceMutex.WaitOne(TimeSpan.FromSeconds(10));
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }

    /// <summary>Abre uma nova instância como administrador e fecha esta. Retorna false se o UAC for recusado.</summary>
    private bool RestartElevated()
    {
        var exePath = Environment.ProcessPath;
        if (exePath == null)
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(exePath, WaitForPreviousArg) { UseShellExecute = true, Verb = "runas" });
        }
        catch (Win32Exception)
        {
            // Usuário recusou o UAC.
            return false;
        }

        MainWindow?.Close();
        return true;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppPaths.LogError(e.Exception);
        e.Handled = true;
    }
}
