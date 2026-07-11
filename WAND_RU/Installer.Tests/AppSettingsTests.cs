using System.IO;
using WandRuInstaller.Core;
using Xunit;

namespace WandRuInstaller.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Save_then_load_roundtrips()
    {
        var path = Path.Combine(Path.GetTempPath(), "wandru-settings-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            new AppSettings { RestartWandAfter = true, ShowLog = true, TranslateCheatsOnline = true }.Save(path);
            var loaded = AppSettings.Load(path);
            Assert.True(loaded.RestartWandAfter);
            Assert.True(loaded.ShowLog);
            Assert.True(loaded.TranslateCheatsOnline);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Load_missing_file_returns_defaults()
    {
        var loaded = AppSettings.Load(Path.Combine(Path.GetTempPath(), "nope-" + Guid.NewGuid().ToString("N") + ".json"));
        Assert.False(loaded.RestartWandAfter);
        Assert.False(loaded.ShowLog);
        Assert.True(loaded.TranslateCheats);       // default on
        Assert.False(loaded.TranslateCheatsOnline); // default off (эталон)
    }
}
