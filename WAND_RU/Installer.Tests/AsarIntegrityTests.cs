using System.IO;
using System.Text;
using WandRuInstaller.Core;
using Xunit;

namespace WandRuInstaller.Tests;

/// <summary>
/// Юнит-тесты синхронизации integrity-хэша (Electron fuse) на синтетике - без 223МБ Wand.exe.
/// Регресс на «патченый Wand молча не стартует»: хэш заголовка asar должен попасть в exe.
/// </summary>
public class AsarIntegrityTests
{
    // Минимальный asar: 16-байтовый size-заголовок (headerLen в [12..16]) + строка-заголовок.
    static string WriteFakeAsar(string dir, string header)
    {
        var headerBytes = Encoding.UTF8.GetBytes(header);
        var size = new byte[16];
        BitConverter.GetBytes(headerBytes.Length).CopyTo(size, 12);
        var path = Path.Combine(dir, "app.asar");
        using var fs = File.Create(path);
        fs.Write(size);
        fs.Write(headerBytes);
        fs.Write(new byte[64]); // немного «payload», не влияет на хэш заголовка
        return path;
    }

    static string WriteExeWithBlob(string dir, string name, string valueHash)
    {
        var blob = "\x7fELF....junk...."
                 + "[{\"file\":\"resources\\\\app.asar\",\"alg\":\"SHA256\",\"value\":\"" + valueHash + "\"}]"
                 + "PADDINGXPADDINGXPADDINGX";
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes(blob));
        return path;
    }

    [Fact]
    public void SyncAppDir_writes_current_header_hash_into_exe()
    {
        var dir = NewDir();
        try
        {
            var res = Path.Combine(dir, "resources");
            Directory.CreateDirectory(res);
            var asar = WriteFakeAsar(res, "{\"files\":{\"a.js\":{\"size\":1,\"offset\":\"0\"}}}");
            var exe = WriteExeWithBlob(dir, "Wand.exe", new string('0', 64));

            var n = AsarIntegrity.SyncAppDir(dir, asar);

            Assert.Equal(1, n);
            var expected = AsarIntegrity.ComputeHeaderHash(asar);
            Assert.Equal(expected, AsarIntegrity.ReadHash(exe));
            Assert.Matches("^[0-9a-f]{64}$", expected);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void SyncAppDir_is_noop_when_no_blob_present()
    {
        var dir = NewDir();
        try
        {
            var res = Path.Combine(dir, "resources");
            Directory.CreateDirectory(res);
            var asar = WriteFakeAsar(res, "{\"files\":{}}");
            var exe = Path.Combine(dir, "Launcher.exe");
            File.WriteAllBytes(exe, Encoding.ASCII.GetBytes("no integrity blob here"));

            Assert.Equal(0, AsarIntegrity.SyncAppDir(dir, asar));
            Assert.Null(AsarIntegrity.ReadHash(exe));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void VerifyExesMatch_throws_when_exe_hash_stale()
    {
        var dir = NewDir();
        try
        {
            var res = Path.Combine(dir, "resources");
            Directory.CreateDirectory(res);
            var asar = WriteFakeAsar(res, "{\"files\":{\"a.js\":{\"size\":1,\"offset\":\"0\"}}}");
            WriteExeWithBlob(dir, "Wand.exe", new string('0', 64)); // хэш не соответствует asar

            var ex = Assert.Throws<InvalidOperationException>(() => AsarIntegrity.VerifyExesMatch(dir, asar));
            Assert.Contains("не запустится", ex.Message);

            AsarIntegrity.SyncAppDir(dir, asar);                    // записали правильный хэш
            AsarIntegrity.VerifyExesMatch(dir, asar);               // теперь не кидает
        }
        finally { Directory.Delete(dir, true); }
    }

    // Общая точка патча и отката: пишем хэш и тут же читаем обратно. Раньше read-back был только
    // в Apply - откат мог отрапортовать успех, оставив Wand с несходящимся хэшем (тихий не-старт).
    [Fact]
    public void SyncAndVerify_writes_hash_and_passes_readback()
    {
        var dir = NewDir();
        try
        {
            var res = Path.Combine(dir, "resources");
            Directory.CreateDirectory(res);
            var asar = WriteFakeAsar(res, "{\"files\":{\"a.js\":{\"size\":1,\"offset\":\"0\"}}}");
            var exe = WriteExeWithBlob(dir, "Wand.exe", new string('0', 64)); // стартовый хэш чужой

            AsarIntegrity.SyncAndVerify(dir, asar);

            Assert.Equal(AsarIntegrity.ComputeHeaderHash(asar), AsarIntegrity.ReadHash(exe));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void SyncAndVerify_is_noop_on_old_wand_without_blob()
    {
        var dir = NewDir();
        try
        {
            var res = Path.Combine(dir, "resources");
            Directory.CreateDirectory(res);
            var asar = WriteFakeAsar(res, "{\"files\":{}}");
            File.WriteAllBytes(Path.Combine(dir, "Launcher.exe"), Encoding.ASCII.GetBytes("no integrity blob here"));

            var log = new List<string>();
            AsarIntegrity.SyncAndVerify(dir, asar, log.Add);   // старая версия Wand: не кидаем

            Assert.Contains(log, l => l.Contains("не обнаружена"));
        }
        finally { Directory.Delete(dir, true); }
    }

    static string NewDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "wru-int-" + Path.GetRandomFileName());
        Directory.CreateDirectory(d);
        return d;
    }
}
