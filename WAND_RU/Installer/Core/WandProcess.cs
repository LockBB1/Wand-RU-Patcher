using System.Diagnostics;

namespace WandRuInstaller.Core;

/// <summary>Детект запущенного Wand — патч/откат невозможны, пока exe залочены (напр. WandAuxilaryService.exe).</summary>
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
}
