using System.ComponentModel;
using System.Diagnostics;

namespace Pulso.Services;

public enum DriverInstallResult
{
    Installed,
    RebootRequired,
    Cancelled,
    WingetMissing,
    Failed,
}

/// <summary>Instala o driver PawnIO (necessário para sensores da CPU) usando o pacote oficial do winget.</summary>
public static class DriverInstaller
{
    public const string PawnIoPackageId = "namazso.PawnIO";
    public const string PawnIoHomepage = "https://pawnio.eu/";

    // Códigos de saída do winget (HRESULT como int).
    private const int WingetRebootRequiredToFinish = unchecked((int)0x8A150109);
    private const int WingetRebootInitiated = unchecked((int)0x8A15010A);
    private const int WingetInstallCancelled = unchecked((int)0x8A150103);
    private const int WingetAlreadyInstalled = unchecked((int)0x8A15002B);
    private const int ErrorCancelled = 1223;
    private const int ErrorSuccessRebootRequired = 3010;

    /// <summary>
    /// Roda o instalador. O próprio instalador pede elevação (UAC); se o usuário recusar, retorna <see cref="DriverInstallResult.Cancelled"/>.
    /// </summary>
    public static async Task<DriverInstallResult> InstallPawnIoAsync()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "winget",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in new[]
                 {
                     "install", "--id", PawnIoPackageId, "--exact", "--silent",
                     "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity",
                 })
        {
            startInfo.ArgumentList.Add(arg);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return DriverInstallResult.WingetMissing;
            }

            await process.WaitForExitAsync();
            return process.ExitCode switch
            {
                0 or WingetAlreadyInstalled => DriverInstallResult.Installed,
                ErrorSuccessRebootRequired or WingetRebootRequiredToFinish or WingetRebootInitiated => DriverInstallResult.RebootRequired,
                ErrorCancelled or WingetInstallCancelled => DriverInstallResult.Cancelled,
                _ => LogFailure(process.ExitCode),
            };
        }
        catch (Win32Exception)
        {
            // winget não encontrado no PATH (App Installer ausente).
            return DriverInstallResult.WingetMissing;
        }
    }

    public static void OpenHomepage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(PawnIoHomepage) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppPaths.LogError(ex);
        }
    }

    private static DriverInstallResult LogFailure(int exitCode)
    {
        AppPaths.LogError(new InvalidOperationException($"winget install {PawnIoPackageId} saiu com código 0x{exitCode:X8}"));
        return DriverInstallResult.Failed;
    }
}
