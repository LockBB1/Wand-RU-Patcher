using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WandRuInstaller.Core.Runtime;

internal sealed class RuntimeScope(string appDir, int port)
{
    readonly string[] _pages = new[] { "index.html", "overlay.html" }
        .Select(name => Path.GetFullPath(Path.Combine(appDir, "resources", "app.asar", name))).ToArray();

    public bool TryGetAppSocket(JsonElement target, out Uri? socket)
    {
        socket = null;
        try
        {
            if (target.GetProperty("type").GetString() != "page" ||
                !Uri.TryCreate(target.GetProperty("url").GetString(), UriKind.Absolute, out var file) ||
                !file.IsFile || file.Host.Length != 0 ||
                !_pages.Contains(Path.GetFullPath(file.LocalPath), StringComparer.OrdinalIgnoreCase)) return false;
            if (!Uri.TryCreate(target.GetProperty("webSocketDebuggerUrl").GetString(), UriKind.Absolute, out var address) ||
                address.Scheme != "ws" || address.Host is not ("127.0.0.1" or "[::1]" or "::1") ||
                address.Port != port || address.UserInfo.Length != 0 || address.Query.Length != 0 || address.Fragment.Length != 0 ||
                !Regex.IsMatch(address.AbsolutePath, @"^/devtools/page/[a-zA-Z0-9_-]+$")) return false;
            socket = address;
            return true;
        }
        catch (Exception error) when (error is KeyNotFoundException or InvalidOperationException or ArgumentException)
        { return false; }
    }

    public static bool OwnsListener(string snapshot, int port, int processId)
    {
        if (port is < 1 or > 65535 || processId <= 0) return false;
        var found = false;
        foreach (var line in snapshot.Split('\n'))
        {
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 3 || fields[0] != "TCP" || !fields[1].EndsWith(":" + port, StringComparison.Ordinal) ||
                !fields[2].EndsWith(":0", StringComparison.Ordinal)) continue;
            if (fields.Length != 5 || fields[1] != $"127.0.0.1:{port}" && fields[1] != $"[::1]:{port}" ||
                !int.TryParse(fields[4], out var owner) || owner != processId) return false;
            found = true;
        }
        return found;
    }

    public static async Task<bool> OwnsListenerAsync(int port, int processId, CancellationToken cancellationToken = default)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "netstat.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("-ano");
        start.ArgumentList.Add("-p");
        start.ArgumentList.Add("tcp");
        using var process = Process.Start(start) ?? throw new IOException("Не удалось проверить локальное соединение Wand.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await Task.WhenAll(output, error).ConfigureAwait(false);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return process.ExitCode == 0 && OwnsListener(await output.ConfigureAwait(false), port, processId);
        }
        finally { if (!process.HasExited) process.Kill(); }
    }
}
