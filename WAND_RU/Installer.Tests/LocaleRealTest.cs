using System.IO;
using System.Linq;
using AsarSharp;
using WandRuInstaller.Core;
using WandRuInstaller.Models;
using Xunit;

namespace WandRuInstaller.Tests;

/// <summary>BuildRuJson на РЕАЛЬНОМ en-US Wand: кириллица должна быть литеральной (не \uXXXX).</summary>
public class LocaleRealTest
{
    [Fact]
    public void BuildRuJson_on_real_enUS_writes_literal_cyrillic()
    {
        var res = Path.Combine(TestPaths.LatestAppDir(), "resources");
        var src = File.Exists(Path.Combine(res, "app.asar.backup"))
            ? Path.Combine(res, "app.asar.backup") : Path.Combine(res, "app.asar");
        var tmp = Path.Combine(Path.GetTempPath(), "loc-" + Guid.NewGuid().ToString("N"));
        AsarExtractor.ExtractAll(src, tmp);
        var enUs = Directory.GetFiles(tmp, "en-US.json", SearchOption.AllDirectories).First();

        var ru = LocaleBuilder.BuildRuJson(File.ReadAllText(enUs), RuOverrides.LoadEmbedded());

        Assert.Contains("Главная", ru);          // литеральная кириллица, не Г...
        Assert.DoesNotContain("\\u0413", ru);
    }
}
