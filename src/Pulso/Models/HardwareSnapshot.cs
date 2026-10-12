namespace Pulso.Models;

/// <summary>Leitura pontual de todos os sensores exibidos pelo widget. Valores nulos = sensor indisponível.</summary>
public sealed record HardwareSnapshot(
    CpuInfo? Cpu,
    GpuInfo? Gpu,
    MemoryInfo Memory,
    IReadOnlyList<DiskInfo> Disks);

public sealed record CpuInfo(string Name, double? Load, double? Temperature, double? ClockMhz);

public sealed record GpuInfo(string Name, double? Load, double? Temperature, double? MemoryUsedMb, double? MemoryTotalMb);

public sealed record MemoryInfo(double TotalGb, double UsedGb)
{
    public double Percent => TotalGb > 0 ? UsedGb / TotalGb * 100 : 0;
}

public sealed record DiskInfo(string Name, double? UsedSpace, double? Temperature);
