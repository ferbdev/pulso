using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulso.Models;
using Pulso.Services;

namespace Pulso.ViewModels;

public sealed record DiskRow(string Name, double Percent, string UsedText, double? Temperature, string TemperatureText);

public sealed partial class WidgetViewModel : ObservableObject, IDisposable
{
    private const string NotAvailable = "N/D";

    private readonly IHardwareMonitorService _monitor;
    private readonly SettingsService _settingsService;
    private readonly WidgetSettings _settings;
    private readonly DispatcherTimer _timer;
    private bool _refreshing;
    private bool _disposed;

    [ObservableProperty] private string _cpuName = "CPU";
    [ObservableProperty] private double _cpuLoad;
    [ObservableProperty] private string _cpuLoadText = NotAvailable;
    [ObservableProperty] private double? _cpuTemperature;
    [ObservableProperty] private string _cpuTemperatureText = NotAvailable;
    [ObservableProperty] private string _cpuClockText = NotAvailable;

    [ObservableProperty] private bool _hasGpu;
    [ObservableProperty] private string _gpuName = "GPU";
    [ObservableProperty] private double _gpuLoad;
    [ObservableProperty] private string _gpuLoadText = NotAvailable;
    [ObservableProperty] private double? _gpuTemperature;
    [ObservableProperty] private string _gpuTemperatureText = NotAvailable;
    [ObservableProperty] private double _gpuMemoryPercent;
    [ObservableProperty] private string _gpuMemoryText = NotAvailable;

    [ObservableProperty] private double _memoryPercent;
    [ObservableProperty] private string _memoryText = NotAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDiskSectionVisible))]
    private IReadOnlyList<DiskRow> _disks = [];

    [ObservableProperty] private string? _statusMessage = "Carregando sensores…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusActionText))]
    [NotifyCanExecuteChangedFor(nameof(FixStatusCommand))]
    private StatusAction _statusAction;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FixStatusCommand))]
    private bool _isFixing;

    /// <summary>Resultado da última tentativa de instalação; tem prioridade sobre a mensagem padrão.</summary>
    private string? _installNotice;

    /// <summary>Pedido para reiniciar o app elevado. Retorna false se o usuário recusar o UAC.</summary>
    public Func<bool>? RestartElevatedRequested { get; set; }

    public string? StatusActionText => StatusAction switch
    {
        StatusAction.InstallDriver => "Instalar driver",
        StatusAction.RestartElevated => "Reiniciar como admin",
        StatusAction.OpenDriverPage => "Abrir site do PawnIO",
        _ => null,
    };

    [ObservableProperty] private bool _topmost;
    [ObservableProperty] private double _widgetOpacity;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDiskSectionVisible))]
    private bool _showDisks;

    public bool IsDiskSectionVisible => ShowDisks && Disks.Count > 0;

    public WidgetViewModel(IHardwareMonitorService monitor, SettingsService settingsService, WidgetSettings settings)
    {
        _monitor = monitor;
        _settingsService = settingsService;
        _settings = settings;

        _topmost = settings.Topmost;
        _widgetOpacity = Math.Clamp(settings.Opacity, 0.3, 1.0);
        _showDisks = settings.ShowDisks;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(250, settings.RefreshIntervalMs)) };
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    public async void Start()
    {
        // Abrir o LibreHardwareMonitor pode levar alguns segundos: fora da thread da UI.
        await Task.Run(_monitor.Open);
        if (_disposed)
        {
            return;
        }

        await RefreshAsync();
        _timer.Start();
    }

    public void SavePosition(double left, double top)
    {
        _settings.Left = left;
        _settings.Top = top;
        _settingsService.Save(_settings);
    }

    [RelayCommand]
    private void SetOpacity(string value) =>
        WidgetOpacity = double.Parse(value, CultureInfo.InvariantCulture);

    [RelayCommand]
    private void SetRefreshInterval(string milliseconds)
    {
        var ms = int.Parse(milliseconds, CultureInfo.InvariantCulture);
        _timer.Interval = TimeSpan.FromMilliseconds(ms);
        _settings.RefreshIntervalMs = ms;
        _settingsService.Save(_settings);
    }

    partial void OnTopmostChanged(bool value)
    {
        _settings.Topmost = value;
        _settingsService.Save(_settings);
    }

    partial void OnWidgetOpacityChanged(double value)
    {
        _settings.Opacity = value;
        _settingsService.Save(_settings);
    }

    partial void OnShowDisksChanged(bool value)
    {
        _settings.ShowDisks = value;
        _settingsService.Save(_settings);
    }

    private async Task RefreshAsync()
    {
        if (_refreshing || _disposed)
        {
            return;
        }

        _refreshing = true;
        try
        {
            var snapshot = await Task.Run(_monitor.GetSnapshot);
            if (!_disposed)
            {
                Apply(snapshot);
            }
        }
        catch (Exception ex)
        {
            AppPaths.LogError(ex);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void Apply(HardwareSnapshot snapshot)
    {
        if (snapshot.Cpu is { } cpu)
        {
            CpuName = cpu.Name;
            CpuLoad = cpu.Load ?? 0;
            CpuLoadText = FormatPercent(cpu.Load);
            CpuTemperature = cpu.Temperature;
            CpuTemperatureText = FormatTemperature(cpu.Temperature);
            CpuClockText = cpu.ClockMhz is { } mhz ? $"{mhz / 1000:0.00} GHz" : NotAvailable;
        }

        HasGpu = snapshot.Gpu != null;
        if (snapshot.Gpu is { } gpu)
        {
            GpuName = gpu.Name;
            GpuLoad = gpu.Load ?? 0;
            GpuLoadText = FormatPercent(gpu.Load);
            GpuTemperature = gpu.Temperature;
            GpuTemperatureText = FormatTemperature(gpu.Temperature);

            if (gpu is { MemoryUsedMb: { } used, MemoryTotalMb: { } total } && total > 0)
            {
                GpuMemoryPercent = used / total * 100;
                GpuMemoryText = $"{used / 1024:0.0} / {total / 1024:0.0} GB";
            }
            else
            {
                GpuMemoryPercent = 0;
                GpuMemoryText = NotAvailable;
            }
        }

        MemoryPercent = snapshot.Memory.Percent;
        MemoryText = $"{snapshot.Memory.UsedGb:0.0} / {snapshot.Memory.TotalGb:0.0} GB";

        Disks = snapshot.Disks
            .Select(d => new DiskRow(d.Name, d.UsedSpace ?? 0, FormatPercent(d.UsedSpace), d.Temperature, FormatTemperature(d.Temperature)))
            .ToList();

        UpdateStatus(snapshot);
    }

    private void UpdateStatus(HardwareSnapshot snapshot)
    {
        if (IsFixing)
        {
            return;
        }

        if (snapshot.Cpu?.Temperature != null)
        {
            SetStatus(null, StatusAction.None);
        }
        else if (!_monitor.IsDriverInstalled)
        {
            SetStatus(_installNotice ?? "Temperatura da CPU requer o driver PawnIO", _installNotice == null ? StatusAction.InstallDriver : StatusAction);
        }
        else if (!_monitor.IsElevated)
        {
            SetStatus("Temperatura da CPU requer administrador", StatusAction.RestartElevated);
        }
        else
        {
            SetStatus(_installNotice, StatusAction.None);
        }
    }

    private void SetStatus(string? message, StatusAction action)
    {
        StatusMessage = message;
        StatusAction = action;
    }

    private bool CanFixStatus() => StatusAction != StatusAction.None && !IsFixing;

    [RelayCommand(CanExecute = nameof(CanFixStatus))]
    private async Task FixStatusAsync()
    {
        switch (StatusAction)
        {
            case StatusAction.InstallDriver:
                await InstallDriverAsync();
                break;
            case StatusAction.RestartElevated:
                if (RestartElevatedRequested?.Invoke() == false)
                {
                    StatusMessage = "Elevação cancelada";
                }

                break;
            case StatusAction.OpenDriverPage:
                DriverInstaller.OpenHomepage();
                break;
        }
    }

    private async Task InstallDriverAsync()
    {
        IsFixing = true;
        StatusMessage = "Instalando PawnIO… confirme o pedido do Windows";
        try
        {
            var result = await DriverInstaller.InstallPawnIoAsync();
            switch (result)
            {
                case DriverInstallResult.Installed:
                    _installNotice = null;
                    StatusMessage = "Driver instalado, recarregando sensores…";
                    await Task.Run(_monitor.Reopen);
                    break;
                case DriverInstallResult.RebootRequired:
                    _installNotice = "Driver instalado: reinicie o Windows para concluir";
                    SetStatus(_installNotice, StatusAction.None);
                    break;
                case DriverInstallResult.Cancelled:
                    _installNotice = null;
                    break;
                case DriverInstallResult.WingetMissing:
                    _installNotice = "winget não encontrado: instale o PawnIO pelo site";
                    SetStatus(_installNotice, StatusAction.OpenDriverPage);
                    break;
                default:
                    _installNotice = "Falha ao instalar o driver (detalhes em error.log)";
                    SetStatus(_installNotice, StatusAction.OpenDriverPage);
                    break;
            }
        }
        finally
        {
            IsFixing = false;
        }

        await RefreshAsync();
    }

    private static string FormatPercent(double? value) => value is { } v ? $"{v:0}%" : NotAvailable;

    private static string FormatTemperature(double? value) => value is { } v ? $"{v:0} °C" : NotAvailable;

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
    }
}
