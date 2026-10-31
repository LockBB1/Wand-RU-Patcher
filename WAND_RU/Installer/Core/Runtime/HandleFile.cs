using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace WandRuInstaller.Core.Runtime;
internal sealed class HandleFile : IDisposable
{
    readonly FileStream _stream;
    public string Path { get; private set; }
    HandleFile(string path, FileStream stream) { Path = path; _stream = stream; }
    public static HandleFile Open(string path) => Open(path, 0x80010000u, 3, FileAccess.Read);
    public static HandleFile OpenMutable(string path) => Open(path, 0xC0010000u, 3, FileAccess.ReadWrite);
    public static HandleFile CreateEmpty(string path) => Open(path, 0xC0010000u, 1, FileAccess.ReadWrite);
    public static HandleFile CreateBackup(string path) => Open(path, 0xC00D0000u, 1, FileAccess.ReadWrite);
    static HandleFile Open(string path, uint access, uint disposition, FileAccess streamAccess)
    {
        var flags = 0x02200000u | (streamAccess == FileAccess.ReadWrite ? 0x80000000u : 0u);
        var handle = CreateFileW(path, access, 1, IntPtr.Zero, disposition, flags, IntPtr.Zero);
        if (handle.IsInvalid) { var error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
        try
        {
            if (!GetFileInformationByHandleEx(handle, 9, out var attributes, 8)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if ((attributes.FileAttributes & 0x400) != 0) throw new InvalidDataException("Ссылка вместо файла Wand.");
            if (!SamePath(handle, path)) throw new InvalidDataException("Путь файла Wand изменился.");
            return new HandleFile(path, new FileStream(handle, streamAccess));
        }
        catch
        {
            try { if (disposition == 1) Delete(handle); }
            finally { handle.Dispose(); }
            throw;
        }
    }
    public void CopyFrom(HandleFile source)
    {
        source._stream.Position = 0;
        _stream.Position = 0;
        source._stream.CopyTo(_stream);
        _stream.Flush(true);
    }
    public void CopyBackupFrom(HandleFile source)
    {
        source._stream.Position = 0;
        var sourceHandle = source._stream.SafeFileHandle;
        var destinationHandle = _stream.SafeFileHandle;
        if (!SetFilePointerEx(sourceHandle, 0, out _, 0) || !SetFilePointerEx(destinationHandle, 0, out _, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var buffer = new byte[65536];
        var readContext = IntPtr.Zero;
        var writeContext = IntPtr.Zero;
        try
        {
            while (true)
            {
                if (!BackupRead(sourceHandle, buffer, (uint)buffer.Length, out var read, false, false, ref readContext)) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (read == 0) break;
                if (!BackupWrite(destinationHandle, buffer, read, out var written, false, false, ref writeContext) || written != read) throw new IOException("Не удалось сохранить резервную копию Wand.");
            }
            var security = new FileSecurity();
            security.SetSecurityDescriptorBinaryForm(source._stream.GetAccessControl().GetSecurityDescriptorBinaryForm(), AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group);
            _stream.SetAccessControl(security);
            if (Security() != source.Security()) throw new InvalidDataException("Не удалось сохранить права резервной копии Wand.");
            _stream.Flush(true);
        }
        finally
        {
            BackupRead(sourceHandle, buffer, 0, out _, true, false, ref readContext);
            BackupWrite(destinationHandle, buffer, 0, out _, true, false, ref writeContext);
            if (!SetFilePointerEx(sourceHandle, 0, out _, 0) || !SetFilePointerEx(destinationHandle, 0, out _, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
            source._stream.Position = 0;
            _stream.Position = 0;
        }
    }
    string Security() => _stream.GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group);
    public void Rename(string destination)
    {
        var name = Encoding.Unicode.GetBytes(System.IO.Path.GetFullPath(destination));
        var rootOffset = IntPtr.Size == 8 ? 8 : 4;
        var lengthOffset = rootOffset + IntPtr.Size;
        var nameOffset = lengthOffset + 4;
        var size = nameOffset + name.Length + 8;
        var memory = Marshal.AllocHGlobal(size);
        try
        {
            for (int i = 0; i < size; i++) Marshal.WriteByte(memory, i, 0);
            Marshal.WriteInt32(memory, lengthOffset, name.Length);
            Marshal.Copy(name, 0, IntPtr.Add(memory, nameOffset), name.Length);
            if (!SetFileInformationByHandle(_stream.SafeFileHandle, 3, memory, (uint)size)) throw new Win32Exception(Marshal.GetLastWin32Error());
            Path = destination;
            if (!SamePath(_stream.SafeFileHandle, destination)) throw new InvalidDataException("Путь файла Wand изменился.");
        }
        finally { Marshal.FreeHGlobal(memory); }
    }
    public bool Trusted()
    {
        var path = Marshal.StringToCoTaskMemUni(System.IO.Path.GetFullPath(Path));
        var file = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), Path = path, Handle = _stream.SafeFileHandle.DangerousGetHandle() };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
        Marshal.StructureToPtr(file, pointer, false);
        var data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), Ui = 2, Union = 1, File = pointer, StateAction = 1, Flags = 0x1010 };
        var policy = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        try { return WinVerifyTrust(new IntPtr(-1), ref policy, ref data) == 0; }
        finally { data.StateAction = 2; WinVerifyTrust(new IntPtr(-1), ref policy, ref data); Marshal.FreeHGlobal(pointer); Marshal.FreeCoTaskMem(path); }
    }
    public string Hash() { _stream.Position = 0; return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(_stream)); }
    public uint LinkCount() => LinkCount(_stream);
    internal static uint LinkCount(FileStream stream)
    {
        if (!GetFileInformationByHandle(stream.SafeFileHandle, out var info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return info.Links;
    }
    internal static RootLease LockRoot(string path)
    {
        var handle = CreateFileW(path, 0x81, 3, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (handle.IsInvalid) { var error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
        try
        {
            if (!GetFileInformationByHandleEx(handle, 9, out var attributes, 8) || !GetFileIdInfo(handle, 18, out var id, 24))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if ((attributes.FileAttributes & 0x400) != 0) throw new InvalidDataException("Папка Wand является ссылкой.");
            if (id.Low == 0 && id.High == 0) throw new InvalidDataException("Не удалось определить идентификатор папки Wand.");
            if (!SamePath(handle, path)) throw new InvalidDataException("Путь папки Wand изменился.");
            return new RootLease(handle, $"{id.Volume:X16}{id.High:X16}{id.Low:X16}");
        }
        catch { handle.Dispose(); throw; }
    }
    internal sealed class RootLease(SafeFileHandle handle, string identity) : IDisposable
    {
        public string Identity { get; } = identity;
        public void Dispose() => handle.Dispose();
    }
    public string ArchiveHash()
    {
        _stream.Position = 0;
        Span<byte> prefix = stackalloc byte[16];
        _stream.ReadExactly(prefix);
        var length = BitConverter.ToInt32(prefix[12..]);
        if (length < 2 || length > 16 * 1024 * 1024 || length > _stream.Length - prefix.Length) throw new InvalidDataException("Повреждён заголовок app.asar.");
        var header = new byte[length];
        _stream.ReadExactly(header);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(header)).ToLowerInvariant();
    }
    public static void VerifyStream(FileStream stream, string expected)
    {
        if (!GetFileInformationByHandleEx(stream.SafeFileHandle, 9, out var attributes, 8)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if ((attributes.FileAttributes & 0x400) != 0 || !SamePath(stream.SafeFileHandle, expected)) throw new InvalidDataException("Путь файла Wand изменился.");
    }
    public byte[] ReadBounded(int maximum)
    {
        if (_stream.Length > maximum) throw new InvalidDataException("Слишком большой файл восстановления Wand.");
        _stream.Position = 0;
        var bytes = new byte[(int)_stream.Length];
        _stream.ReadExactly(bytes);
        return bytes;
    }
    public void WriteAll(byte[] bytes)
    {
        _stream.Position = 0;
        _stream.SetLength(0);
        _stream.Write(bytes);
        _stream.Flush(true);
    }
    public void Flush() => _stream.Flush(true);
    public void Delete() => Delete(_stream.SafeFileHandle);
    static void Delete(SafeFileHandle handle)
    {
        var memory = Marshal.AllocHGlobal(1);
        try
        {
            Marshal.WriteByte(memory, 0, 1);
            if (!SetFileInformationByHandle(handle, 4, memory, 1)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { Marshal.FreeHGlobal(memory); }
    }
    public static DirectoryHandles LockDirectories(params string[] directories)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in directories)
            for (var item = new DirectoryInfo(System.IO.Path.GetFullPath(directory)); item is not null; item = item.Parent) paths.Add(item.FullName);
        var handles = new List<(string Path, SafeFileHandle Handle)>();
        try
        {
            foreach (var path in paths.OrderBy(path => path.Length))
            {
                var handle = CreateFileW(path, 0x81, 3, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
                if (handle.IsInvalid) { var error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
                if (!GetFileInformationByHandleEx(handle, 9, out var attributes, 8)) { var error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
                if ((attributes.FileAttributes & 0x400) != 0) { handle.Dispose(); throw new InvalidDataException("Ссылка в пути Wand."); }
                if (!SamePath(handle, path)) { handle.Dispose(); throw new InvalidDataException("Путь папки Wand изменился."); }
                handles.Add((path, handle));
            }
            return new DirectoryHandles(handles);
        }
        catch { foreach (var item in handles) item.Handle.Dispose(); throw; }
    }
    internal sealed class DirectoryHandles(List<(string Path, SafeFileHandle Handle)> handles) : IDisposable
    {
        public void Validate()
        {
            foreach (var item in handles)
            {
                if (!GetFileInformationByHandleEx(item.Handle, 9, out var attributes, 8)) throw new Win32Exception(Marshal.GetLastWin32Error());
                if ((attributes.FileAttributes & 0x400) != 0 || !SamePath(item.Handle, item.Path)) throw new InvalidDataException("Путь папки Wand изменился.");
            }
        }
        public void Dispose() { foreach (var item in handles) item.Handle.Dispose(); }
    }
    static bool SamePath(SafeFileHandle handle, string expected)
    {
        var value = new StringBuilder(32768);
        var length = GetFinalPathNameByHandleW(handle, value, (uint)value.Capacity, 0);
        if (length == 0 || length >= value.Capacity) throw new Win32Exception(Marshal.GetLastWin32Error());
        var actual = value.ToString();
        if (actual.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) actual = @"\\" + actual[8..];
        else if (actual.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)) actual = actual[4..];
        if (expected.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) expected = @"\\" + expected[8..];
        else if (expected.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)) expected = expected[4..];
        return string.Equals(System.IO.Path.TrimEndingDirectorySeparator(actual), System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(expected)), StringComparison.OrdinalIgnoreCase);
    }
    public string? EmbeddedHash()
    {
        var anchor = Encoding.ASCII.GetBytes(@"resources\\app.asar");
        var key = Encoding.ASCII.GetBytes("\"value\":\"");
        var position = AsarIntegrity.FindPattern(_stream, anchor, 0, 0);
        if (position < 0) return null;
        position = AsarIntegrity.FindPattern(_stream, key, position, position + 200);
        if (position < 0) return null;
        _stream.Position = position + key.Length;
        var bytes = new byte[64];
        _stream.ReadExactly(bytes);
        return Encoding.ASCII.GetString(bytes);
    }
    public bool WriteEmbeddedHash(string value, bool existingMayBeTorn = false)
    {
        if (value.Length != 64 || !value.All(Uri.IsHexDigit) || EmbeddedHash() is not { } previous || (!existingMayBeTorn && !previous.All(Uri.IsHexDigit))) return false;
        _stream.Position -= 64;
        _stream.Write(Encoding.ASCII.GetBytes(value));
        _stream.Flush(true);
        return true;
    }
    public void Dispose() => _stream.Dispose();
    [StructLayout(LayoutKind.Sequential)] struct TrustFile { public uint Size; public IntPtr Path, Handle, Subject; }
    [StructLayout(LayoutKind.Sequential)] struct AttributeTag { public uint FileAttributes, ReparseTag; }
    [StructLayout(LayoutKind.Sequential)] struct FileInformation { public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh, Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [StructLayout(LayoutKind.Sequential)] struct FileId { public ulong Volume, Low, High; }
    [StructLayout(LayoutKind.Sequential)] struct TrustData { public uint Size; public IntPtr Policy, Sip; public uint Ui, Revocation, Union; public IntPtr File; public uint StateAction; public IntPtr State, Url; public uint Flags, Context; public IntPtr SignatureSettings; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass, IntPtr information, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int informationClass, out AttributeTag information, uint size);
    [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", SetLastError = true)] static extern bool GetFileIdInfo(SafeFileHandle handle, int informationClass, out FileId information, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool BackupRead(SafeFileHandle handle, byte[] buffer, uint length, out uint read, bool abort, bool security, ref IntPtr context);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool BackupWrite(SafeFileHandle handle, byte[] buffer, uint length, out uint written, bool abort, bool security, ref IntPtr context);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetFilePointerEx(SafeFileHandle handle, long distance, out long position, uint moveMethod);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path, uint length, uint flags);
    [DllImport("wintrust.dll", ExactSpelling = true)] static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
}
