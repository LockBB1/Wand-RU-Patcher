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
        Action<string>? log = null, CancellationToken cancellationToken = default, (string Path, string Identity)? activation = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var appDir = Path.GetDirectoryName(Path.GetFullPath(start.FileName))!;
        if (activation is not null)
        {
            if (HasRunningInstance(Directory.GetParent(appDir)!.FullName))
                throw new InvalidOperationException("Wand уже запущен. Закройте его перед запуском перевода.");
            RuntimeExeRepair.TryRepair(appDir, Path.GetDirectoryName(activation.Value.Path)!);
        }
        var enhanced = ValidateOriginal(appDir);
        if (enhanced is not null)
        {
            if (activation is null) throw new InvalidOperationException("Сначала установите перевод WRP для Wand-Enhancer.");
            RuntimeEnhancer.Pin(Path.GetDirectoryName(activation.Value.Path)!, RuntimeEnhancer.Snapshot(appDir, enhanced.Value), false);
        }
        if (HasRunningInstance(Directory.GetParent(appDir)!.FullName))
            throw new InvalidOperationException("Wand уже запущен. Закройте его перед запуском перевода.");
        cancellationToken.ThrowIfCancellationRequested();
        int? supervisedPort = null;
        if (enhanced == true)
        {
            supervisedPort = RuntimeSupervised.ReservePort();
            start.ArgumentList[0] = "--remote-debugging-port=" + supervisedPort.Value;
            start.FileName = Path.Combine(Directory.GetParent(appDir)!.FullName, "Wand.exe");
            start.WorkingDirectory = Directory.GetParent(appDir)!.FullName;
        }
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
        if (supervisedPort is null)
            process.Exited += (_, _) => endpoint.TrySetException(new InvalidOperationException("Wand завершился до подключения перевода."));
        var clients = new Dictionary<string, DevToolsClient>();
        var seen = new HashSet<string>();
        var started = false;
        var translated = false;
        var completed = false;
        Process? supervisedChild = null;
        try
        {
            started = process.Start();
            if (!started) throw new IOException("Не удалось запустить Wand.");
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            startup.CancelAfter(TimeSpan.FromSeconds(30));
            Uri address;
            if (supervisedPort is not null)
            {
                var owned = await RuntimeSupervised.WaitAsync(process, Path.Combine(appDir, "Wand.exe"), supervisedPort.Value, startup.Token).ConfigureAwait(false);
                supervisedChild = owned.Child;
                address = owned.Endpoint;
            }
            else address = await endpoint.Task.WaitAsync(startup.Token).ConfigureAwait(false);
            var owner = supervisedChild ?? process;
            if (!await RuntimeScope.OwnsListenerAsync(address.Port, owner.Id, cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException("Не подтверждено безопасное локальное соединение Wand.");
            if (enhanced is not null)
                RuntimeEnhancer.Pin(Path.GetDirectoryName(activation!.Value.Path)!, RuntimeEnhancer.Snapshot(appDir, enhanced.Value), false);
            var actualAppDir = supervisedChild is null ? Path.GetDirectoryName(owner.MainModule!.FileName)! : appDir;
            var scope = new RuntimeScope(actualAppDir, address.Port);
            var script = RuntimeScripts.MainScript(actualAppDir, settings, activation);
            await using var browser = await DevToolsClient.ConnectAsync(address, cancellationToken).ConfigureAwait(false);
            await using var frames = await RuntimeTargets.StartAsync(browser, scope, script, settings, cancellationToken).ConfigureAwait(false);
            using var http = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
            {
                BaseAddress = new UriBuilder("http", address.Host.Trim('[', ']'), address.Port).Uri,
                Timeout = TimeSpan.FromSeconds(3), MaxResponseContentBufferSize = DevToolsClient.MaxMessageBytes
            };
            var firstPageDeadline = DateTime.UtcNow.AddSeconds(30);
            while (!owner.HasExited)
            {
                cancellationToken.ThrowIfCancellationRequested();
                frames.ThrowIfFailed();
                string snapshot;
                try { snapshot = await http.GetStringAsync("json/list", cancellationToken).ConfigureAwait(false); }
                catch (HttpRequestException) when (owner.HasExited) { break; }
                using var targets = JsonDocument.Parse(snapshot);
                var live = targets.RootElement.EnumerateArray().Select(t => t.GetProperty("id").GetString()!).ToHashSet();
                frames.Prune(live);
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
                    if (owner.HasExited || !await RuntimeScope.OwnsListenerAsync(address.Port, owner.Id, cancellationToken).ConfigureAwait(false))
                        throw new IOException("Локальное соединение Wand изменилось.");
                    var page = await AttachPageAsync(socket!, id, http, script, cancellationToken, frames).ConfigureAwait(false);
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
            completed = true;
        }
        finally
        {
            foreach (var client in clients.Values) await client.DisposeAsync().ConfigureAwait(false);
            if (supervisedChild is not null)
            {
                await RuntimeSupervised.StopChildAsync(supervisedChild).ConfigureAwait(false);
                supervisedChild.Dispose();
                if (!completed && !process.HasExited)
                {
                    process.Kill(); // Only the launcher we started; never its process tree.
                    await process.WaitForExitAsync().ConfigureAwait(false);
                }
            }
            else if (supervisedPort is not null && started)
                await RuntimeSupervised.StopFailedLaunchAsync(process, Path.Combine(appDir, "Wand.exe")).ConfigureAwait(false);
            else if (supervisedPort is null && started && !process.HasExited)
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
        HttpClient http, string script, CancellationToken cancellationToken, RuntimeTargets? frames = null)
    {
        DevToolsClient? client = null;
        var attached = false;
        try
        {
            client = await DevToolsClient.ConnectAsync(socket, cancellationToken).ConfigureAwait(false);
            var reload = frames is null ? true : await frames.ShouldReloadAsync(id, cancellationToken).ConfigureAwait(false);
            if (reload is null) return null;
            await client.SendAsync("Page.enable", cancellationToken: cancellationToken).ConfigureAwait(false);
            await client.SendAsync("Page.addScriptToEvaluateOnNewDocument", new { source = script }, cancellationToken).ConfigureAwait(false);
            // Only pre-existing pages need reload; future overlays were armed while paused.
            if (reload.Value) await client.SendAsync("Page.reload", new { ignoreCache = false }, cancellationToken).ConfigureAwait(false);
            var status = await WaitReadyAsync(client, cancellationToken).ConfigureAwait(false);
            attached = true;
            return (client, status);
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested &&
            error is IOException or System.Net.WebSockets.WebSocketException or InvalidOperationException or OperationCanceledException or TimeoutException)
        {
            // A closed overlay is local; a failed hook in a live window still fails the session.
            using var current = JsonDocument.Parse(await http.GetStringAsync("json/list", cancellationToken).ConfigureAwait(false));
            if (current.RootElement.EnumerateArray().Any(t => t.GetProperty("id").GetString() == id)) throw;
            return null;
        }
        finally { if (!attached && client is not null) await client.DisposeAsync().ConfigureAwait(false); }
    }

    internal static bool? ValidateOriginal(string appDir)
    {
        var enhanced = RuntimeEnhancer.Validate(appDir);
        if (enhanced is not null) return enhanced;
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
        return null;
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
