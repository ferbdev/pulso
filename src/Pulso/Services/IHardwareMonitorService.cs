using Pulso.Models;

namespace Pulso.Services;

public interface IHardwareMonitorService : IDisposable
{
    /// <summary>Inicializa os sensores. Pode demorar alguns segundos; chame fora da thread da UI.</summary>
    void Open();

    /// <summary>Fecha e reabre os sensores, por exemplo depois de instalar o driver.</summary>
    void Reopen();

    /// <summary>Atualiza os sensores e devolve a leitura atual. Thread-safe.</summary>
    HardwareSnapshot GetSnapshot();

    /// <summary>Relatório em texto de todo o hardware e sensores detectados (diagnóstico).</summary>
    string GetReport();

    bool IsElevated { get; }

    bool IsDriverInstalled { get; }
}
