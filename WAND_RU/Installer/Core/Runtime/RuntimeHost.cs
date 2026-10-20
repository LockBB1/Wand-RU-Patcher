using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace WandRuInstaller.Core.Runtime;

internal static class RuntimeHost
{
    public static ProcessStartInfo CreateStartInfo(string appDir, IEnumerable<string>? arguments = null)
    {
        var start = new ProcessStartInfo(Path.GetFullPath(Path.Combine(appDir, "Wand.exe")))
        { WorkingDirectory = Path.GetFullPath(appDir), UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("--remote-debugging-port=0");
        start.ArgumentList.Add("--remote-debugging-address=127.0.0.1");
        start.Environment.Remove("NODE_OPTIONS");
        start.Environment.Remove("ELECTRON_RUN_AS_NODE");
        foreach (var argument in arguments ?? Array.Empty<string>())
        {
            var option = argument.TrimStart().TrimStart('-', '/');
            if (option.StartsWith("remote-", StringComparison.OrdinalIgnoreCase) ||
                option.StartsWith("inspect", StringComparison.OrdinalIgnoreCase) ||
                option.StartsWith("js-flags", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Аргументы ярлыка Wand меняют настройки отладки. Уберите их перед запуском перевода.");
            start.ArgumentList.Add(argument);
        }
        return start;
    }

    public static async Task RunAsync(ProcessStartInfo start, AppSettings settings, Action<JsonElement>? ready = null,
        Action<string>? log = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var appDir = Path.GetDirectoryName(Path.GetFullPath(start.FileName))!;
        ValidateOriginal(appDir);
        if (HasRunningInstance(Directory.GetParent(appDir)!.FullName))
            throw new InvalidOperationException("Wand уже запущен. Закройте его перед запуском перевода.");
        cancellationToken.ThrowIfCancellationRequested();
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = start.RedirectStandardError = true;
        start.StandardOutputEncoding = start.StandardErrorEncoding = Encoding.UTF8;
        using var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        var endpoint = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        process.ErrorDataReceived += (_, e) =>
        {
            const string prefix = "DevTools listening on ";
            if (e.Data?.StartsWith(prefix, StringComparison.Ordinal) == true &&
                Uri.TryCreate(e.Data[prefix.Length..].Trim(), UriKind.Absolute, out var address) &&
                address.Scheme == "ws" && address.Host is "127.0.0.1" or "[::1]" or "::1" &&
                address.AbsolutePath.StartsWith("/devtools/browser/", StringComparison.Ordinal)) endpoint.TrySetResult(address);
        };
        process.OutputDataReceived += (_, _) => { };
        process.Exited += (_, _) => endpoint.TrySetException(new InvalidOperationException("Wand завершился до подключения перевода."));
        var clients = new Dictionary<string, DevToolsClient>();
        var seen = new HashSet<string>();
        var started = false;
        var translated = false;
        try
        {
            started = process.Start();
            if (!started) throw new IOException("Не удалось запустить Wand.");
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            startup.CancelAfter(TimeSpan.FromSeconds(30));
            var address = await endpoint.Task.WaitAsync(startup.Token).ConfigureAwait(false);
            if (!await RuntimeScope.OwnsListenerAsync(address.Port, process.Id, cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException("Не подтверждено безопасное локальное соединение Wand.");
            var actualAppDir = Path.GetDirectoryName(process.MainModule!.FileName)!;
            var scope = new RuntimeScope(actualAppDir, address.Port);
            var script = RuntimeScripts.MainScript(actualAppDir, settings);
            using var http = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
            {
                BaseAddress = new UriBuilder("http", address.Host.Trim('[', ']'), address.Port).Uri,
                Timeout = TimeSpan.FromSeconds(3), MaxResponseContentBufferSize = DevToolsClient.MaxMessageBytes
            };
            var firstPageDeadline = DateTime.UtcNow.AddSeconds(30);
            while (!process.HasExited)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string snapshot;
                try { snapshot = await http.GetStringAsync("json/list", cancellationToken).ConfigureAwait(false); }
                catch (HttpRequestException) when (process.HasExited) { break; }
                using var targets = JsonDocument.Parse(snapshot);
                var live = targets.RootElement.EnumerateArray().Select(t => t.GetProperty("id").GetString()!).ToHashSet();
                foreach (var id in clients.Keys.Where(id => !live.Contains(id)).ToArray())
                {
                    await clients[id].DisposeAsync().ConfigureAwait(false);
                    clients.Remove(id);
                    seen.Remove(id);
                }
                foreach (var target in targets.RootElement.EnumerateArray())
                {
                    if (!scope.TryGetAppSocket(target, out var socket)) continue;
                    var id = target.GetProperty("id").GetString()!;
                    if (!seen.Add(id)) continue;
                    if (process.HasExited || !await RuntimeScope.OwnsListenerAsync(address.Port, process.Id, cancellationToken).ConfigureAwait(false))
                        throw new IOException("Локальное соединение Wand изменилось.");
                    var page = await AttachPageAsync(socket!, id, http, script, cancellationToken).ConfigureAwait(false);
                    if (page is null) { seen.Remove(id); continue; }
                    clients.Add(id, page.Value.Client);
                    translated = true;
                    ready?.Invoke(page.Value.Status);
                    log?.Invoke($"Перевод интерфейса готов: {page.Value.Status.GetProperty("keys").GetInt32()} ключей.");
                }
                if (!translated && DateTime.UtcNow > firstPageDeadline)
                    throw new InvalidOperationException("Окно Wand не подтвердило готовность перевода.");
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            foreach (var client in clients.Values) await client.DisposeAsync().ConfigureAwait(false);
            if (started && !process.HasExited)
            {
                process.CloseMainWindow();
                using var close = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try { await process.WaitForExitAsync(close.Token).ConfigureAwait(false); }
                catch (OperationCanceledException)
                {
                    if (!process.HasExited) process.Kill(); // Only the helper's own app, never a game/process tree.
                    await process.WaitForExitAsync().ConfigureAwait(false);
                }
            }
        }
    }

    internal static async Task<(DevToolsClient Client, JsonElement Status)?> AttachPageAsync(Uri socket, string id,
        HttpClient http, string script, CancellationToken cancellationToken)
    {
        DevToolsClient? client = null;
        var attached = false;
        try
        {
            client = await DevToolsClient.ConnectAsync(socket, cancellationToken).ConfigureAwait(false);
            await client.SendAsync("Page.enable", cancellationToken: cancellationToken).ConfigureAwait(false);
            await client.SendAsync("Page.addScriptToEvaluateOnNewDocument", new { source = script }, cancellationToken).ConfigureAwait(false);
            // Install fetch hooks before trainer navigation in this newly owned session.
            await client.SendAsync("Page.reload", new { ignoreCache = false }, cancellationToken).ConfigureAwait(false);
            var status = await WaitReadyAsync(client, cancellationToken).ConfigureAwait(false);
            attached = true;
            return (client, status);
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested &&
            error is IOException or System.Net.WebSockets.WebSocketException or InvalidOperationException or OperationCanceledException)
        {
            // A closed overlay is local; a failed hook in a live window still fails the session.
            using var current = JsonDocument.Parse(await http.GetStringAsync("json/list", cancellationToken).ConfigureAwait(false));
            if (current.RootElement.EnumerateArray().Any(t => t.GetProperty("id").GetString() == id)) throw;
            return null;
        }
        finally { if (!attached && client is not null) await client.DisposeAsync().ConfigureAwait(false); }
    }

    internal static void ValidateOriginal(string appDir)
    {
        var executable = Path.Combine(appDir, "Wand.exe");
        var rootExecutable = Path.Combine(Directory.GetParent(appDir)!.FullName, "Wand.exe");
        if (!Authenticode.IsTrusted(executable) || !Authenticode.IsTrusted(rootExecutable))
            throw new InvalidOperationException("Подпись Wand не подтверждена. Сначала восстановите оригинальные файлы Wand.");
        var archive = Path.Combine(appDir, "resources", "app.asar");
        using (var stream = File.OpenRead(archive))
        {
            Span<byte> prefix = stackalloc byte[16];
            stream.ReadExactly(prefix);
            var length = BitConverter.ToInt32(prefix[12..]);
            if (length < 2 || length > stream.Length - prefix.Length) throw new InvalidDataException("Повреждён заголовок app.asar.");
        }
        var embedded = AsarIntegrity.ReadHash(executable);
        if (embedded is null || embedded != AsarIntegrity.ComputeHeaderHash(archive))
            throw new InvalidOperationException("Целостность Wand не подтверждена. Восстановите оригинальные файлы Wand.");
    }

    static bool HasRunningInstance(string root)
    {
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        foreach (var process in Process.GetProcessesByName("Wand").Concat(Process.GetProcessesByName("WeMod")))
        {
            using (process)
            {
                try
                {
                    if (process.MainModule?.FileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true) return true;
                }
                catch (Win32Exception) { return true; }
                catch (InvalidOperationException) { }
            }
        }
        return false;
    }

    static async Task<JsonElement> WaitReadyAsync(DevToolsClient client, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        while (true)
        {
            JsonElement response;
            try
            {
                response = await client.SendAsync("Runtime.evaluate", new
                {
                    expression = "window.__wandRuRuntimeReady || null", awaitPromise = true, returnByValue = true
                }, timeout.Token).ConfigureAwait(false);
            }
            catch (InvalidOperationException) { await Task.Delay(200, timeout.Token).ConfigureAwait(false); continue; }
            if (response.TryGetProperty("exceptionDetails", out _))
                throw new InvalidOperationException("Служба перевода не поддерживает эту версию Wand.");
            if (response.TryGetProperty("result", out var result) && result.TryGetProperty("value", out var value) &&
                value.ValueKind == JsonValueKind.Object && value.TryGetProperty("ui", out var ui) && ui.ValueKind == JsonValueKind.True)
                return value.Clone();
            await Task.Delay(200, timeout.Token).ConfigureAwait(false);
        }
    }
}
