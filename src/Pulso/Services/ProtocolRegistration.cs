using Microsoft.Win32;

namespace Pulso.Services;

/// <summary>
/// Registra o esquema <c>pulso://</c> para o usuário atual (HKCU, sem admin), apontando para o executável
/// em uso. Assim o site consegue abrir o widget com <c>pulso://open</c>. Como o app é portátil e pode ser
/// movido, o caminho é atualizado a cada inicialização.
/// </summary>
public static class ProtocolRegistration
{
    public const string Scheme = "pulso";

    public static void EnsureRegistered()
    {
        var exePath = Environment.ProcessPath;
        if (exePath == null)
        {
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Scheme}");
            key.SetValue(string.Empty, "URL:Pulso");
            key.SetValue("URL Protocol", string.Empty);

            using var icon = key.CreateSubKey("DefaultIcon");
            icon.SetValue(string.Empty, $"\"{exePath}\",0");

            using var command = key.CreateSubKey(@"shell\open\command");
            command.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");
        }
        catch (Exception ex)
        {
            AppPaths.LogError(ex);
        }
    }
}
