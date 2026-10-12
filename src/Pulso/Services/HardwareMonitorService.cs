using System.Runtime.InteropServices;
using System.Security.Principal;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;
using Pulso.Models;

namespace Pulso.Services;

/// <summary>Envolve o <see cref="Computer"/> do LibreHardwareMonitor e traduz os sensores para <see cref="HardwareSnapshot"/>.</summary>
public sealed class HardwareMonitorService : IHardwareMonitorService
{
    private readonly object _sync = new();
    private readonly UpdateVisitor _visitor = new();
    private Computer? _computer;
    private bool _disposed;

    public bool IsElevated { get; } =
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    /// <summary>
    /// Lê o registro a cada chamada: <c>PawnIo.IsInstalled</c> da lib é calculado uma única vez
    /// e não enxerga uma instalação feita com o app já aberto.
    /// </summary>
    public bool IsDriverInstalled
    {
        get
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
                return key?.GetValue("DisplayVersion") != null;
            }
            catch (Exception ex)
            {
                AppPaths.LogError(ex);
                return false;
            }
        }
    }

    public void Open()
    {
        lock (_sync)
        {
            if (_computer != null || _disposed)
            {
                return;
            }

            try
            {
                var computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    // Sem admin os discos não retornam sensores; evita o custo de varrê-los.
                    IsStorageEnabled = IsElevated,
                };
                computer.Open();
                _computer = computer;
            }
            catch (Exception ex)
            {
                AppPaths.LogError(ex);
            }
        }
    }

    public void Reopen()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _computer?.Close();
            }
            catch (Exception ex)
            {
                AppPaths.LogError(ex);
            }

            _computer = null;
            Open();
        }
    }

    public HardwareSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            CpuInfo? cpu = null;
            GpuInfo? gpu = null;
            var disks = new List<DiskInfo>();

            if (_computer != null && !_disposed)
            {
                _computer.Accept(_visitor);
                var hardware = _computer.Hardware;

                var cpuHardware = hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
                if (cpuHardware != null)
                {
                    cpu = ReadCpu(cpuHardware);
                }

                var gpuHardware = PickGpu(hardware);
                if (gpuHardware != null)
                {
                    gpu = ReadGpu(gpuHardware);
                }

                disks.AddRange(hardware.Where(h => h.HardwareType == HardwareType.Storage).Select(ReadDisk));
            }

            return new HardwareSnapshot(cpu, gpu, ReadMemory(), disks);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _computer?.Close();
            }
            catch (Exception ex)
            {
                AppPaths.LogError(ex);
            }

            _computer = null;
        }
    }

    private static CpuInfo ReadCpu(IHardware cpu)
    {
        var sensors = AllSensors(cpu).ToList();

        var temperature = Find(sensors, SensorType.Temperature, "CPU Package", "Core (Tctl/Tdie)", "Core (Tdie)", "Core Average")
            ?? sensors
                .Where(s => s.SensorType == SensorType.Temperature && !s.Name.Contains("Distance", StringComparison.OrdinalIgnoreCase))
                .Select(s => Valid(s.Value))
                .Max();

        var clocks = sensors
            .Where(s => s.SensorType == SensorType.Clock && s.Name.Contains("Core #", StringComparison.OrdinalIgnoreCase))
            .Select(s => Valid(s.Value))
            .OfType<double>()
            .Where(v => v > 0)
            .ToList();

        return new CpuInfo(
            cpu.Name,
            Find(sensors, SensorType.Load, "CPU Total"),
            temperature,
            clocks.Count > 0 ? clocks.Average() : null);
    }

    private static IHardware? PickGpu(IList<IHardware> hardware) =>
        // Prefere a placa dedicada à integrada.
        hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia)
            ?? hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuAmd)
            ?? hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuIntel);

    private static GpuInfo ReadGpu(IHardware gpu)
    {
        var sensors = AllSensors(gpu).ToList();
        return new GpuInfo(
            gpu.Name,
            Find(sensors, SensorType.Load, "GPU Core", "D3D 3D"),
            Find(sensors, SensorType.Temperature, "GPU Core", "GPU Hot Spot"),
            BytesToMb(Find(sensors, SensorType.Data, "GPU Memory Used", "D3D Dedicated Memory Used")),
            BytesToMb(Find(sensors, SensorType.Data, "GPU Memory Total", "D3D Dedicated Memory Total")));
    }

    // Desde a 0.9.7, os sensores de memória (SensorType.Data) vêm em bytes.
    private static double? BytesToMb(double? bytes) => bytes / (1024d * 1024d);

    private static DiskInfo ReadDisk(IHardware disk)
    {
        var sensors = AllSensors(disk).ToList();
        var temperature = Find(sensors, SensorType.Temperature, "Temperature", "Composite Temperature")
            ?? sensors
                .Where(s => s.SensorType == SensorType.Temperature)
                .Select(s => Valid(s.Value))
                .FirstOrDefault(v => v.HasValue);

        return new DiskInfo(disk.Name, Find(sensors, SensorType.Load, "Used Space"), temperature);
    }

    private static MemoryInfo ReadMemory()
    {
        // API do Windows: não depende de driver nem de privilégio de administrador.
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            return new MemoryInfo(0, 0);
        }

        const double bytesPerGb = 1024d * 1024 * 1024;
        var total = status.TotalPhys / bytesPerGb;
        var available = status.AvailPhys / bytesPerGb;
        return new MemoryInfo(total, total - available);
    }

    private static IEnumerable<ISensor> AllSensors(IHardware hardware) =>
        hardware.Sensors.Concat(hardware.SubHardware.SelectMany(AllSensors));

    private static double? Find(IReadOnlyCollection<ISensor> sensors, SensorType type, params string[] names)
    {
        foreach (var name in names)
        {
            var value = Valid(sensors.FirstOrDefault(s => s.SensorType == type && s.Name == name)?.Value);
            if (value.HasValue)
            {
                return value;
            }
        }

        return null;
    }

    private static double? Valid(float? value) =>
        value is { } v && !float.IsNaN(v) && !float.IsInfinity(v) ? v : null;

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);

        public void VisitHardware(IHardware hardware)
        {
            try
            {
                hardware.Update();
            }
            catch (Exception ex)
            {
                // Um sensor com falha não pode derrubar a leitura dos outros.
                AppPaths.LogError(ex);
            }

            foreach (var sub in hardware.SubHardware)
            {
                sub.Accept(this);
            }
        }

        public void VisitSensor(ISensor sensor)
        {
        }

        public void VisitParameter(IParameter parameter)
        {
        }
    }
}
