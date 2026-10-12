#if DEBUG
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pulso.Models;
using Pulso.Services;
using Pulso.ViewModels;
using Pulso.Views;

namespace Pulso;

/// <summary>
/// Ferramenta de documentação (apenas Debug): <c>Pulso.exe --snapshot &lt;pasta&gt;</c> renderiza o card
/// em PNG transparente (2x) para o README e encerra.
/// </summary>
public partial class App
{
    private const string SnapshotArg = "--snapshot";
    private const double SnapshotScale = 2;

    private bool TryRunSnapshot(string[] args)
    {
        var index = Array.IndexOf(args, SnapshotArg);
        if (index < 0)
        {
            return false;
        }

        var outputDirectory = Path.GetFullPath(index + 1 < args.Length ? args[index + 1] : "snapshots");
        _ = RunSnapshotAsync(outputDirectory);
        return true;
    }

    private async Task RunSnapshotAsync(string outputDirectory)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            Directory.CreateDirectory(outputDirectory);

            var settingsService = new SettingsService();
            var userSettings = settingsService.Load();
            var settings = new WidgetSettings { Opacity = 1.0, ShowDisks = true };

            _monitor = new HardwareMonitorService();
            _viewModel = new WidgetViewModel(_monitor, settingsService, settings);

            var window = new WidgetWindow(_viewModel, settings) { ShowActivated = false };
            MainWindow = window;
            window.Show();
            _viewModel.Start();

            // Deixa os sensores acumularem algumas leituras.
            await Task.Delay(TimeSpan.FromSeconds(7));
            SavePng(window.Card, Path.Combine(outputDirectory, "widget.png"));

            _viewModel.ShowDisks = false;
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            SavePng(window.Card, Path.Combine(outputDirectory, "widget-compact.png"));

            // A janela e o ViewModel gravam settings.json; fecha primeiro e depois devolve o que o usuário tinha.
            window.Close();
            settingsService.Save(userSettings);
        }
        catch (Exception ex)
        {
            AppPaths.LogError(ex);
        }
        finally
        {
            _viewModel?.Dispose();
            _viewModel = null;
            Shutdown();
        }
    }

    private static void SavePng(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        var width = (int)Math.Ceiling(element.ActualWidth * SnapshotScale);
        var height = (int)Math.Ceiling(element.ActualHeight * SnapshotScale);

        var bitmap = new RenderTargetBitmap(width, height, 96 * SnapshotScale, 96 * SnapshotScale, PixelFormats.Pbgra32);
        bitmap.Render(element);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
#endif
