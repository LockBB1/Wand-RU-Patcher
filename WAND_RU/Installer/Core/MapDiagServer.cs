using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace WandRuInstaller.Core;

/// <summary>
/// Локальный диагностический приёмник для Path-D map-хука (Шаг 1 PoC). Пропатченный Wand
/// (main-процесс, o.net) шлёт POST-строки на 127.0.0.1:Port, отдаём их в лог инсталлера -
/// оттуда готовый Copy/Export. TcpListener (не HttpListener): не требует urlacl/админ-прав на
/// loopback + сам факт ошибки записи виден в логе (в отличие от молчаливого fs в чужом процессе).
/// PoC-инструмент: перед релизом убрать/загейтить.
/// </summary>
public sealed class MapDiagServer : IDisposable
{
    public const int Port = 39271;

    static readonly Regex ContentLen = new(@"Content-Length:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    readonly Action<string> _onLine;
    readonly TcpListener _listener = new(IPAddress.Loopback, Port);
    volatile bool _run;

    public MapDiagServer(Action<string> onLine) => _onLine = onLine;

    /// <summary>Старт приёмника. false + строка в логе, если порт занят (не кидает).</summary>
    public bool Start()
    {
        try { _listener.Start(); _run = true; _ = Task.Run(Loop); return true; }
        catch (Exception e) { _onLine($"[map-diag] не слушаю :{Port} - {e.Message}"); return false; }
    }

    async Task Loop()
    {
        while (_run)
        {
            TcpClient cli;
            try { cli = await _listener.AcceptTcpClientAsync(); }
            catch { break; }
            _ = Task.Run(() => Handle(cli));
        }
    }

    void Handle(TcpClient cli)
    {
        try
        {
            using (cli)
            using (var ns = cli.GetStream())
            {
                var buf = new byte[65536];
                int total = 0, headerEnd = -1, contentLen = 0;
                while (total < buf.Length)
                {
                    int n = ns.Read(buf, total, buf.Length - total);
                    if (n <= 0) break;
                    total += n;
                    if (headerEnd < 0)
                    {
                        var head = Encoding.ASCII.GetString(buf, 0, total);
                        int idx = head.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                        if (idx >= 0)
                        {
                            headerEnd = idx + 4;
                            var m = ContentLen.Match(head);
                            contentLen = m.Success ? int.Parse(m.Groups[1].Value) : 0;
                        }
                    }
                    if (headerEnd >= 0 && total >= headerEnd + contentLen) break;
                }
                var resp = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                try { ns.Write(resp, 0, resp.Length); } catch { /* клиент мог отвалиться */ }
                if (headerEnd >= 0 && contentLen > 0)
                {
                    var body = Encoding.UTF8.GetString(buf, headerEnd, Math.Min(contentLen, total - headerEnd));
                    if (!string.IsNullOrWhiteSpace(body)) _onLine(body.TrimEnd());
                }
            }
        }
        catch { /* диагностика best-effort */ }
    }

    public void Dispose()
    {
        _run = false;
        try { _listener.Stop(); } catch { }
    }
}
