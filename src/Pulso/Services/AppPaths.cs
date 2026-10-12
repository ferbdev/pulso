using System.IO;

namespace Pulso.Services;

public static class AppPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pulso");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static string ErrorLogFile => Path.Combine(DataDirectory, "error.log");

    public static void LogError(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            File.AppendAllText(ErrorLogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}");
        }
        catch
        {
            // Sem onde registrar; ignora.
        }
    }
}
