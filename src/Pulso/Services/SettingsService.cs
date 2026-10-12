using System.IO;
using System.Text.Json;
using Pulso.Models;

namespace Pulso.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public WidgetSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                return JsonSerializer.Deserialize<WidgetSettings>(json, JsonOptions) ?? new WidgetSettings();
            }
        }
        catch (Exception ex)
        {
            AppPaths.LogError(ex);
        }

        return new WidgetSettings();
    }

    public void Save(WidgetSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (Exception ex)
        {
            AppPaths.LogError(ex);
        }
    }
}
