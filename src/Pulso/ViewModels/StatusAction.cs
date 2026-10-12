namespace Pulso.ViewModels;

/// <summary>Ação oferecida ao lado do aviso de status para resolver o problema.</summary>
public enum StatusAction
{
    None,
    InstallDriver,
    RestartElevated,
    OpenDriverPage,
}
