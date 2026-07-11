using System.Diagnostics;

namespace WandRuInstaller.Core;

/// <summary>Детект запущенного Wand - патч/откат невозможны, пока exe залочены (напр. WandAuxilaryService.exe).</summary>
public static class WandProcess
{
    static readonly string[] Names =
    {
        "Wand", "WeMod",
        "WandAuxilaryService", "WandAuxiliaryService", // обе орфографии на всякий
    };

    public static bool AnyRunning()
    {
        foreach (var name in Names)
        {
            var procs = Process.GetProcessesByName(name);
            try { if (procs.Length > 0) return true; }
            finally { foreach (var p in procs) p.Dispose(); }
        }
        return false;
    }

    /// <summary>Закрыть все процессы Wand (перед патчем/откатом при включённом авто-перезапуске).</summary>
    public static void KillAll()
    {
        foreach (var name in Names)
            foreach (var p in Process.GetProcessesByName(name))
            {
                try { p.Kill(entireProcessTree: true); p.WaitForExit(5000); }
                catch { /* уже закрыт / нет прав */ }
                finally { p.Dispose(); }
            }
    }
}
