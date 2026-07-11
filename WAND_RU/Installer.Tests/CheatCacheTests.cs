using System.IO;
using WandRuInstaller.Core;
using Xunit;

namespace WandRuInstaller.Tests;

public class CheatCacheTests
{
    [Fact]
    public void Size_and_count_reflect_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "wandru-cache-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, "{\"unlimited widgets\":\"Бесконечные виджеты\",\"quantum flux\":\"Квантовый поток\"}");
            Assert.Equal(2, CheatCache.EntryCount(path));
            Assert.True(CheatCache.SizeBytes(path) > 0);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Missing_file_is_zero()
    {
        var path = Path.Combine(Path.GetTempPath(), "nope-cache-" + Guid.NewGuid().ToString("N") + ".json");
        Assert.Equal(0, CheatCache.EntryCount(path));
        Assert.Equal(0, CheatCache.SizeBytes(path));
    }

    [Fact]
    public void Clear_removes_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "wandru-cache-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, "{\"a\":\"б\"}");
        CheatCache.Clear(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Corrupt_file_counts_zero_but_has_size()
    {
        var path = Path.Combine(Path.GetTempPath(), "wandru-cache-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, "{ not json");
            Assert.Equal(0, CheatCache.EntryCount(path)); // не падает
            Assert.True(CheatCache.SizeBytes(path) > 0);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
