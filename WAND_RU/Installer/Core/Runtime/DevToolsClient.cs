using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;

namespace WandRuInstaller.Core.Runtime;

internal sealed class DevToolsClient : IAsyncDisposable
{
    public const int MaxMessageBytes = 16 * 1024 * 1024;
    readonly ClientWebSocket _socket;
    readonly SemaphoreSlim _writer = new(1, 1);
    readonly CancellationTokenSource _lifetime = new();
    readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    readonly Task _reader;
    int _next;

    public event Action<string, JsonElement, string?>? EventReceived;

    DevToolsClient(ClientWebSocket socket)
    {
        _socket = socket;
        _reader = ReadAsync();
    }

    public static async Task<DevToolsClient> ConnectAsync(Uri address, CancellationToken cancellationToken = default)
    {
        if (!address.IsAbsoluteUri || address.Scheme != "ws" ||
            address.Host is not ("127.0.0.1" or "[::1]" or "::1") ||
            address.Port <= 0 || address.UserInfo.Length != 0 ||
            !address.AbsolutePath.StartsWith("/devtools/", StringComparison.Ordinal))
            throw new ArgumentException("DevTools должен принадлежать локальному процессу Wand.", nameof(address));
        var socket = new ClientWebSocket();
        socket.Options.Proxy = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await socket.ConnectAsync(address, timeout.Token).ConfigureAwait(false);
            return new DevToolsClient(socket);
        }
        catch { socket.Dispose(); throw; }
    }

    public async Task<JsonElement> SendAsync(string method, object? parameters = null, CancellationToken cancellationToken = default,
        string? sessionId = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var id = Interlocked.Increment(ref _next);
        var reply = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = reply;
        try
        {
            var command = new Dictionary<string, object> { ["id"] = id, ["method"] = method, ["params"] = parameters ?? new { } };
            if (sessionId is not null) command["sessionId"] = sessionId;
            var bytes = JsonSerializer.SerializeToUtf8Bytes(command);
            await _writer.WaitAsync(timeout.Token).ConfigureAwait(false);
            try
            {
                if (_socket.State != WebSocketState.Open) throw new IOException("Соединение с Wand закрыто.");
                await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
            }
            finally { _writer.Release(); }
            return await reply.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        finally { _pending.TryRemove(id, out _); }
    }

    async Task ReadAsync()
    {
        var buffer = new byte[16384];
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                using var body = new MemoryStream();
                WebSocketReceiveResult frame;
                do
                {
                    frame = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _lifetime.Token).ConfigureAwait(false);
                    if (frame.MessageType != WebSocketMessageType.Text) throw new IOException("Соединение с Wand закрыто.");
                    if (body.Length + frame.Count > MaxMessageBytes) throw new IOException("Слишком большой ответ DevTools.");
                    body.Write(buffer, 0, frame.Count);
                } while (!frame.EndOfMessage);
                using var json = JsonDocument.Parse(body.ToArray());
                var message = json.RootElement;
                if (message.TryGetProperty("id", out var number) && _pending.TryGetValue(number.GetInt32(), out var reply))
                {
                    if (message.TryGetProperty("error", out _))
                    {
                        _pending.TryRemove(number.GetInt32(), out _);
                        reply.TrySetException(new InvalidOperationException("Команда DevTools отклонена."));
                    }
                    else
                    {
                        var result = message.GetProperty("result").Clone();
                        _pending.TryRemove(number.GetInt32(), out _);
                        reply.TrySetResult(result);
                    }
                }
                else if (message.TryGetProperty("method", out var method))
                {
                    var parameters = message.TryGetProperty("params", out var p) ? p.Clone() : default;
                    EventReceived?.Invoke(method.GetString()!, parameters,
                        message.TryGetProperty("sessionId", out var session) ? session.GetString() : null);
                }
            }
        }
        catch (Exception error)
        {
            try { _socket.Abort(); } catch (ObjectDisposedException) { }
            foreach (var reply in _pending.Values)
                reply.TrySetException(new IOException("Соединение с Wand закрыто.", error));
        }
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        _socket.Dispose();
        await _reader.ConfigureAwait(false);
        _lifetime.Dispose();
        _writer.Dispose();
    }
}
