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
            new AppSettings { RestartWandAfter = true, ShowLog = true }.Save(path);
            var loaded = AppSettings.Load(path);
            Assert.True(loaded.RestartWandAfter);
            Assert.True(loaded.ShowLog);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Load_missing_file_returns_defaults()
    {
        var loaded = AppSettings.Load(Path.Combine(Path.GetTempPath(), "nope-" + Guid.NewGuid().ToString("N") + ".json"));
        Assert.False(loaded.RestartWandAfter);
        Assert.False(loaded.ShowLog);
    }
}
