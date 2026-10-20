using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace WandRuInstaller.Core.Runtime;

internal sealed class RuntimeTargets : IAsyncDisposable
{
    readonly DevToolsClient _client;
    readonly RuntimeScope _scope;
    readonly string _script;
    readonly AppSettings _settings;
    readonly CancellationTokenSource _lifetime;
    readonly Channel<(string Method, JsonElement Parameters, string? Session)> _events =
        Channel.CreateBounded<(string, JsonElement, string?)>(512);
    readonly Channel<(Session Session, Context Context, string[] Texts)> _translations =
        Channel.CreateBounded<(Session, Context, string[])>(8);
    readonly ConcurrentDictionary<string, Session> _sessions = new();
    readonly ConcurrentDictionary<string, ArmingState> _arming = new();
    readonly ConcurrentDictionary<string, TaskCompletionSource> _attachments = new();
    readonly object _attachmentGate = new();
    readonly Task _reader, _translator;
    Exception? _failure;
    bool _disposed;

    sealed class ArmingState
    {
        public readonly TaskCompletionSource<bool?> Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public volatile bool Closed;
        public Exception? Error;
    }

    sealed class Session(string id, string target, string type, string? parent, string url)
    {
        public string Id = id, Target = target, Type = type, Url = url;
        public string? Parent = parent, MainFrame;
        public readonly object NavigationGate = new();
        public ConcurrentDictionary<int, Context> Contexts = new();
    }

    sealed class Context(int id, string uniqueId)
    {
        public int Id = id;
        public string UniqueId = uniqueId;
        public string? Frame, AppPage, Address;
        public bool Injected, IsMap;
    }

    RuntimeTargets(DevToolsClient client, RuntimeScope scope, string script, AppSettings settings, CancellationToken token)
    {
        (_client, _scope, _script, _settings) = (client, scope, script, settings);
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        _client.EventReceived += Receive;
        _reader = ReadAsync();
        _translator = TranslateAsync();
    }

    public static async Task<RuntimeTargets> StartAsync(DevToolsClient client, RuntimeScope scope, string script,
        AppSettings settings, CancellationToken token = default)
    {
        var targets = new RuntimeTargets(client, scope, script, settings, token);
        try
        {
            await client.SendAsync("Target.setAutoAttach", AutoAttach("page"), token).ConfigureAwait(false);
            return targets;
        }
        catch { await targets.DisposeAsync().ConfigureAwait(false); throw; }
    }

    static object AutoAttach(string type) => new
    {
        autoAttach = true, waitForDebuggerOnStart = true, flatten = true,
        filter = new object[] { new { type, exclude = false }, new { exclude = true } }
    };

    ArmingState Arming(string target) => _arming.GetOrAdd(target, _ => new ArmingState());

    public async Task<bool?> ShouldReloadAsync(string target, CancellationToken token = default)
    {
        var state = Arming(target);
        if (state.Closed) return null;
        var prearmed = await state.Ready.Task.WaitAsync(TimeSpan.FromSeconds(20), token).ConfigureAwait(false);
        if (state.Closed) return null;
        if (state.Error is { } error) throw new IOException("Не удалось подготовить окно Wand.", error);
        return prearmed.HasValue ? !prearmed.Value : null;
    }

    public void Prune(IReadOnlySet<string> live)
    {
        foreach (var pair in _arming)
            if (pair.Value.Closed && !live.Contains(pair.Key)) _arming.TryRemove(pair.Key, out _);
    }
    public void ThrowIfFailed()
    {
        if (Volatile.Read(ref _failure) is { } error) throw new IOException("Не удалось подготовить окно Wand для перевода.", error);
    }

    void Receive(string method, JsonElement parameters, string? session)
    {
        if (method == "Target.detachedFromTarget")
        {
            if (_sessions.TryRemove(parameters.GetProperty("sessionId").GetString()!, out var removed))
            {
                var arming = Arming(removed.Target);
                arming.Closed = true;
                arming.Ready.TrySetResult(null);
            }
            return;
        }
        if (method == "Target.attachedToTarget")
        {
            lock (_attachmentGate)
            {
                if (_disposed) return;
                var id = parameters.GetProperty("sessionId").GetString()!;
                var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _attachments[id] = done;
                _ = AttachTrackedAsync(parameters, session, id, done);
            }
            return;
        }
        if (method is not ("Target.attachedToTarget" or "Target.detachedFromTarget" or "Page.frameNavigated" or
            "Runtime.executionContextCreated" or "Runtime.executionContextDestroyed" or "Runtime.executionContextsCleared" or "Fetch.requestPaused" or "Runtime.consoleAPICalled")) return;
        if (method == "Runtime.consoleAPICalled" && (!parameters.TryGetProperty("args", out var args) ||
            args.GetArrayLength() != 1 || !args[0].TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.String ||
            value.GetString() is not { Length: <= 64000 } text || !text.StartsWith("WANDRU_MTREQ::", StringComparison.Ordinal))) return;
        if (!_events.Writer.TryWrite((method, parameters, session)))
            Interlocked.CompareExchange(ref _failure, new IOException("Слишком много событий перевода Wand."), null);
    }

    async Task AttachTrackedAsync(JsonElement parameters, string? parent, string id, TaskCompletionSource done)
    {
        try { await AttachAsync(parameters, parent).ConfigureAwait(false); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error) { Interlocked.CompareExchange(ref _failure, error, null); }
        finally { _attachments.TryRemove(id, out _); done.TrySetResult(); }
    }

    async Task<JsonElement> Send(string method, object? parameters, string session)
    {
        try { return await _client.SendAsync(method, parameters, _lifetime.Token, session).ConfigureAwait(false); }
        catch (OperationCanceledException error) when (!_lifetime.IsCancellationRequested)
        { throw new IOException("Команда перевода не завершилась: " + method, error); }
    }

    bool Owned(Session session)
    {
        // Parent sessions come from browser autoattach, never a page-supplied URL or opener.
        for (var depth = 0; depth < 32; depth++)
        {
            if (session.Parent is null) return session.Type == "page" && _scope.IsAppPage(session.Url);
            if (!_sessions.TryGetValue(session.Parent, out session!)) return false;
        }
        return false;
    }

    async Task ReadAsync()
    {
        try
        {
            await foreach (var item in _events.Reader.ReadAllAsync(_lifetime.Token).ConfigureAwait(false))
            {
                if (item.Session is null || !_sessions.TryGetValue(item.Session, out var session)) continue;
                try { await HandleAsync(session, item.Method, item.Parameters).ConfigureAwait(false); }
                catch (Exception error) when (error is IOException or InvalidOperationException or OperationCanceledException)
                {
                    // Remote frame navigation/closure is local, like the legacy map hook.
                    if (item.Method == "Fetch.requestPaused") throw;
                }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error) { Interlocked.CompareExchange(ref _failure, error, null); }
    }

    async Task AttachAsync(JsonElement parameters, string? parent)
    {
        var id = parameters.GetProperty("sessionId").GetString()!;
        var info = parameters.GetProperty("targetInfo");
        var session = new Session(id, info.GetProperty("targetId").GetString()!, info.GetProperty("type").GetString()!, parent,
            info.GetProperty("url").GetString()!);
        var arming = _arming.AddOrUpdate(session.Target, _ => new ArmingState(), (_, current) => current.Closed ? new ArmingState() : current);
        Exception? failure = null;
        var resumed = false;
        async Task Resume()
        {
            using var resume = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try { await _client.SendAsync("Runtime.runIfWaitingForDebugger", cancellationToken: resume.Token, sessionId: id).ConfigureAwait(false); resumed = true; }
            catch (Exception error) when (error is IOException or InvalidOperationException or OperationCanceledException) { }
        }
        try
        {
            if (session.Type is not ("page" or "iframe")) return;
            _sessions[id] = session;
            if (session.Type == "page")
            {
                await Send("Page.addScriptToEvaluateOnNewDocument", new { source = _script }, id).ConfigureAwait(false);
            }
            // Page.enable can wait for a paused child; resume each target independently first.
            await Resume().ConfigureAwait(false);
            if (_settings.TranslateMaps)
            {
                await Send("Page.enable", null, id).ConfigureAwait(false);
                var tree = await Send("Page.getFrameTree", null, id).ConfigureAwait(false);
                var frame = tree.GetProperty("frameTree").GetProperty("frame");
                lock (session.NavigationGate)
                    if (session.MainFrame is null)
                    {
                        session.MainFrame = frame.GetProperty("id").GetString();
                        session.Url = frame.GetProperty("url").GetString()!;
                    }
                await Send("Runtime.enable", null, id).ConfigureAwait(false);
                await Send("Target.setAutoAttach", AutoAttach("iframe"), id).ConfigureAwait(false);
                await Send("Fetch.enable", new { patterns = new[]
                {
                    new { urlPattern = "https://wand.com/ru/maps/*", resourceType = "Document", requestStage = "Request" },
                    new { urlPattern = "https://www.wand.com/ru/maps/*", resourceType = "Document", requestStage = "Request" },
                    new { urlPattern = "https://mist.wand.com/ru/assistant/embed*", resourceType = "Document", requestStage = "Request" }
                }}, id).ConfigureAwait(false);
            }
        }
        catch (Exception error) { failure = error; }
        finally
        {
            // No unsupported or closing target may remain paused by the helper.
            if (!resumed) await Resume().ConfigureAwait(false);
        }
        if (session.Type == "page")
        {
            if (!arming.Closed && _sessions.TryGetValue(id, out var current) && ReferenceEquals(current, session)) arming.Error = failure;
            arming.Ready.TrySetResult(resumed ? parameters.GetProperty("waitingForDebugger").GetBoolean() : null);
        }
    }

    async Task HandleAsync(Session session, string method, JsonElement parameters)
    {
        if (method == "Page.frameNavigated")
        {
            var frame = parameters.GetProperty("frame");
            var mainChanged = false;
            lock (session.NavigationGate)
            {
                if (!frame.TryGetProperty("parentId", out _) || frame.GetProperty("id").GetString() == session.MainFrame)
                {
                    session.MainFrame = frame.GetProperty("id").GetString();
                    session.Url = frame.GetProperty("url").GetString()!;
                    mainChanged = true;
                }
            }
            if (mainChanged) foreach (var context in session.Contexts.Values) await InjectAsync(session, context).ConfigureAwait(false);
        }
        else if (method == "Runtime.executionContextsCleared") session.Contexts.Clear();
        else if (method == "Runtime.executionContextDestroyed") session.Contexts.TryRemove(parameters.GetProperty("executionContextId").GetInt32(), out _);
        else if (method == "Runtime.executionContextCreated")
        {
            var description = parameters.GetProperty("context");
            if (!description.TryGetProperty("auxData", out var auxiliary) ||
                !auxiliary.TryGetProperty("isDefault", out var isDefault) || isDefault.ValueKind != JsonValueKind.True ||
                !description.TryGetProperty("uniqueId", out var unique)) return;
            var context = new Context(description.GetProperty("id").GetInt32(), unique.GetString()!)
            { Frame = auxiliary.TryGetProperty("frameId", out var frame) ? frame.GetString() : null };
            session.Contexts[context.Id] = context;
            await InjectAsync(session, context).ConfigureAwait(false);
        }
        else if (method == "Fetch.requestPaused")
        {
            var id = parameters.GetProperty("requestId").GetString()!;
            var url = Owned(session) && parameters.GetProperty("resourceType").GetString() == "Document"
                ? RuntimeScripts.EmbedUrl(parameters.GetProperty("request").GetProperty("url").GetString()!) : null;
            try
            {
                await Send("Fetch.continueRequest", url is null ? new { requestId = id } : (object)new { requestId = id, url }, session.Id).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                // Navigation can cancel interception before the command reaches Chromium.
                if (url is null) return;
                try { await Send("Fetch.continueRequest", new { requestId = id }, session.Id).ConfigureAwait(false); }
                catch (InvalidOperationException) { }
            }
        }
        else if (method == "Runtime.consoleAPICalled" && _settings.TranslateMapsOnline && Owned(session) &&
            session.Contexts.TryGetValue(parameters.GetProperty("executionContextId").GetInt32(), out var source) && source.Injected && source.IsMap)
        {
            try
            {
                var current = await Send("Runtime.evaluate", new
                {
                    expression = Guard(source.Address!, "return window!==window.top && window.__wandruTr===1;"),
                    uniqueContextId = source.UniqueId, returnByValue = true
                }, session.Id).ConfigureAwait(false);
                if (!current.GetProperty("result").TryGetProperty("value", out var valid) || valid.ValueKind != JsonValueKind.True) return;
                var encoded = parameters.GetProperty("args")[0].GetProperty("value").GetString()![14..];
                var texts = JsonSerializer.Deserialize<string[]>(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
                if (texts is { Length: > 0 and <= 20 })
                {
                    var accepted = texts.Where(text => text is { Length: > 1 and <= 1500 }).Distinct(StringComparer.Ordinal).ToArray();
                    if (accepted.Length == 0) return;
                    if (!_translations.Writer.TryWrite((session, source, accepted)))
                        await Send("Runtime.evaluate", new
                        {
                            expression = Guard(source.Address!, "setTimeout(function(){window.__wandruRetry && window.__wandruRetry(" + JsonSerializer.Serialize(accepted) + ");},1000);"),
                            uniqueContextId = source.UniqueId
                        }, session.Id).ConfigureAwait(false);
                }
            }
            catch (Exception error) when (error is JsonException or FormatException) { }
        }
    }

    async Task InjectAsync(Session session, Context context)
    {
        if (context.Injected || !Owned(session)) return;
        var location = await Send("Runtime.evaluate", new { expression = "window.location.href", uniqueContextId = context.UniqueId, returnByValue = true }, session.Id).ConfigureAwait(false);
        if (!location.TryGetProperty("result", out var result) || !result.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.String) return;
        var url = value.GetString()!;
        if (session.Type == "page" && context.Frame == session.MainFrame && _scope.IsAppPage(url))
            context.AppPage = new Uri(url).GetLeftPart(UriPartial.Path);
        if (RuntimeScripts.FrameScript(url, _settings) is not { } script) return;
        var response = await Send("Runtime.evaluate", new { expression = script, uniqueContextId = context.UniqueId, returnByValue = true }, session.Id).ConfigureAwait(false);
        if (response.TryGetProperty("exceptionDetails", out _) ||
            !response.GetProperty("result").TryGetProperty("value", out var armed) || armed.ValueKind != JsonValueKind.True) return;
        context.IsMap = RuntimeScripts.IsMap(url);
        context.Address = new Uri(url).GetLeftPart(UriPartial.Path);
        context.Injected = true;
    }

    bool Active(Session session, Context context) => Owned(session) && _sessions.ContainsKey(session.Id) &&
        session.Contexts.TryGetValue(context.Id, out var current) && ReferenceEquals(current, context);

    async Task TranslateAsync()
    {
        try
        {
            await foreach (var batch in _translations.Reader.ReadAllAsync(_lifetime.Token).ConfigureAwait(false))
            {
                var translated = new ConcurrentDictionary<string, string>();
                await Parallel.ForEachAsync(batch.Texts, new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = _lifetime.Token }, async (text, token) =>
                {
                    if (!Active(batch.Session, batch.Context)) return;
                    var bridge = _sessions.Values.Where(session => session.Type == "page" && Owned(session))
                        .SelectMany(session => session.Contexts.Values.Where(context => context.AppPage is not null)
                            .Select(context => (Session: session, Context: context))).FirstOrDefault();
                    if (bridge.Session is null) return;
                    try
                    {
                        var response = await Send("Runtime.evaluate", new
                        {
                            expression = Guard(bridge.Context.AppPage!, "return window.__wandRuTranslateMap ? window.__wandRuTranslateMap(" + JsonSerializer.Serialize(text) + ") : null;"),
                            uniqueContextId = bridge.Context.UniqueId, awaitPromise = true, returnByValue = true
                        }, bridge.Session.Id).ConfigureAwait(false);
                        if (response.TryGetProperty("result", out var result) && result.TryGetProperty("value", out var value) &&
                            value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 and <= 4000 } russian) translated[text] = russian;
                    }
                    catch (Exception error) when (error is IOException or InvalidOperationException or OperationCanceledException) { }
                }).ConfigureAwait(false);
                if (Active(batch.Session, batch.Context))
                {
                    try
                    {
                        await Send("Runtime.evaluate", new
                        {
                            expression = Guard(batch.Context.Address!, "window.__wandruApply && window.__wandruApply(" + JsonSerializer.Serialize(translated) + ");"),
                            uniqueContextId = batch.Context.UniqueId
                        }, batch.Session.Id).ConfigureAwait(false);
                    }
                    catch (Exception error) when (error is IOException or InvalidOperationException or OperationCanceledException) { }
                }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error) { Interlocked.CompareExchange(ref _failure, error, null); }
    }

    static string Guard(string address, string body) =>
        "(function(){var u=new URL(location.href);u.search='';u.hash='';if(u.href!==" + JsonSerializer.Serialize(address) + ")return null;" + body + "})()";

    public async ValueTask DisposeAsync()
    {
        lock (_attachmentGate) _disposed = true;
        _client.EventReceived -= Receive;
        _lifetime.Cancel();
        await Task.WhenAll(_attachments.Values.Select(done => done.Task).Append(_reader).Append(_translator)).ConfigureAwait(false);
        _lifetime.Dispose();
    }
}
