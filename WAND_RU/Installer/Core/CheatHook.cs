using System.IO;
using System.Reflection;

namespace WandRuInstaller.Core;

internal static class CheatHook
{
    const string FileName = "cheat-hook.js";
    public static string LoadEmbedded()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith(FileName, StringComparison.OrdinalIgnoreCase));
        using var s = asm.GetManifestResourceStream(name)!;
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
