namespace Pulso.Models;

public sealed class WidgetSettings
{
    public double? Left { get; set; }
    public double? Top { get; set; }
    public bool Topmost { get; set; } = true;
    public double Opacity { get; set; } = 0.95;
    public int RefreshIntervalMs { get; set; } = 1000;
    public bool ShowDisks { get; set; } = true;
}
