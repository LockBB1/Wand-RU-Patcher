using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace WandRuInstaller.Core.Runtime;

internal static class RuntimeSupervised
{
    internal static int ReservePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try { listener.Start(); return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    internal static async Task<(Process Child, Uri Endpoint)> WaitAsync(Process launcher, string executable, int port, CancellationToken token)
    {
        var expected = Path.GetFullPath(executable);
        Process? child = null;
        using var http = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(2), MaxResponseContentBufferSize = 1024 * 1024 };
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (launcher.HasExited) throw new InvalidOperationException("Загрузчик Wand-Enhancer завершился до запуска Wand.");
                child ??= FindChild(launcher.Id, expected);
                if (child is not null)
                {
                    if (child.HasExited) throw new InvalidOperationException("Wand завершился до подключения перевода.");
                    if (!string.Equals(ImagePath(child), expected, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Wand-Enhancer запустил другую версию Wand.");
                    if (await RuntimeScope.OwnsListenerAsync(port, child.Id, token).ConfigureAwait(false))
                    {
                        try
                        {
                            var json = await http.GetStringAsync($"http://127.0.0.1:{port}/json/version", token).ConfigureAwait(false);
                            using var document = JsonDocument.Parse(json);
                            var raw = document.RootElement.GetProperty("webSocketDebuggerUrl").GetString();
                            if (!Uri.TryCreate(raw, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "ws" ||
                                endpoint.Host != "127.0.0.1" || endpoint.Port != port || endpoint.UserInfo.Length != 0 ||
                                endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
                                !Regex.IsMatch(endpoint.AbsolutePath, @"^/devtools/browser/[a-zA-Z0-9_-]+$") || child.HasExited)
                                throw new InvalidDataException("Не подтверждено безопасное соединение Wand.");
                            var result = child;
                            child = null;
                            return (result, endpoint);
                        }
                        catch (HttpRequestException) { }
                    }
                }
                await Task.Delay(200, token).ConfigureAwait(false);
            }
        }
        finally
        {
            if (child is not null)
            {
                await StopChildAsync(child).ConfigureAwait(false);
                child.Dispose();
            }
        }
    }

    internal static async Task StopChildAsync(Process child)
    {
        if (child.HasExited) return;
        child.CloseMainWindow();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await child.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            if (!child.HasExited) child.Kill();
            await child.WaitForExitAsync().ConfigureAwait(false);
        }
    }

    internal static async Task StopFailedLaunchAsync(Process launcher, string executable)
    {
        if (!launcher.HasExited)
        {
            launcher.Kill();
            await launcher.WaitForExitAsync().ConfigureAwait(false);
        }
        using var child = FindChild(launcher.Id, Path.GetFullPath(executable));
        if (child is not null) await StopChildAsync(child).ConfigureAwait(false);
    }

    static Process? FindChild(int parentId, string expected)
    {
        var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
        Process? found = null;
        try
        {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
            if (!Process32First(snapshot, ref entry)) throw new Win32Exception(Marshal.GetLastWin32Error());
            do
            {
                if (entry.ParentProcessId != parentId || !entry.Executable.Equals("Wand.exe", StringComparison.OrdinalIgnoreCase)) continue;
                Process candidate;
                try { candidate = Process.GetProcessById((int)entry.ProcessId); }
                catch (ArgumentException) { continue; }
                try
                {
                    var path = ImagePath(candidate);
                    var appDir = path is null ? null : Path.GetDirectoryName(path);
                    var root = Directory.GetParent(Path.GetDirectoryName(expected)!)!.FullName;
                    if (candidate.HasExited || appDir is null ||
                        !string.Equals(Path.GetDirectoryName(appDir), root, StringComparison.OrdinalIgnoreCase) ||
                        !Path.GetFileName(appDir).StartsWith("app-", StringComparison.OrdinalIgnoreCase))
                    { candidate.Dispose(); continue; }
                    if (found is not null) { candidate.Dispose(); throw new InvalidOperationException("Не удалось однозначно определить процесс Wand."); }
                    found = candidate;
                }
                catch (Win32Exception error) when (error.NativeErrorCode is 87 or 299) { candidate.Dispose(); continue; }
                catch { candidate.Dispose(); throw; }
            } while (Process32Next(snapshot, ref entry));
            return found;
        }
        catch { found?.Dispose(); throw; }
        finally { CloseHandle(snapshot); }
    }

    static string ImagePath(Process process)
    {
        var path = new StringBuilder(32768);
        var length = path.Capacity;
        if (!QueryFullProcessImageName(process.SafeHandle, 0, path, ref length)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return path.ToString();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ProcessEntry
    {
        public uint Size, Usage, ProcessId;
        public IntPtr DefaultHeapId;
        public uint ModuleId, Threads;
        public int ParentProcessId, BasePriority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Executable;
    }

    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder path, ref int size);
    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool CloseHandle(IntPtr handle);
}
