using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WandRuInstaller.Core;

internal static class RuPatcher
{
    public const string ManifestName = "wand-ru-patch.json";
    const string RuLocaleEntry = "\"ru-RU.json\"";
    internal static bool IsAsarPatched(string asarPath)
    {
        if (!File.Exists(asarPath)) return false;
        try { return AsarIntegrity.ReadHeaderJson(asarPath).Contains(RuLocaleEntry, StringComparison.Ordinal); }
        catch { return false; }
    }

    internal static FileStream AcquireLock(string resources)
    {
        var lockPath = Path.Combine(resources, ".wru-lock");
        try
        {
            try { return new FileStream(lockPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose); }
            catch (IOException) { return OpenExistingLock(lockPath); }
        }
        catch (InvalidDataException) { throw; }
        catch (IOException)
        {
            throw new InvalidOperationException(
                "Другой экземпляр WRP уже патчит или откатывает этот Wand. Дождитесь его завершения.");
        }
    }

    static FileStream OpenExistingLock(string path)
    {
        var handle = CreateFileW(path, 0xC0000000, 0, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            if (error is 2 or 32 or 33) throw new IOException("Не удалось занять блокировку Wand.");
            throw new Win32Exception(error);
        }
        try
        {
            if (!GetFileInformationByHandleEx(handle, 9, out var attributes, 8)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if ((attributes.FileAttributes & 0x400) != 0) throw new InvalidDataException("Файл блокировки Wand является ссылкой.");
            return new FileStream(handle, FileAccess.ReadWrite, 1, false);
        }
        catch { handle.Dispose(); throw; }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct LockAttributes { public uint FileAttributes, ReparseTag; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int informationClass, out LockAttributes information, uint size);

}
