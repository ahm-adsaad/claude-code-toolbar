using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ClaudeToolbar.App.Services;

/// <summary>Minimal loopback HTTP/1.1 server for Claude Code hooks. Bodies of POST /hook are raised on a thread-pool thread.</summary>
public sealed class HookListener : IDisposable
{
    public const int MaxBodyBytes = 64 * 1024;
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(2);

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public event Action<string>? HookReceived;

    public int? Port { get; private set; }
    public string? Error { get; private set; }
    public bool IsListening => _listener is not null && Error is null;

    public void Start(int port)
    {
        Stop();
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            _listener = listener;
            _cts = new CancellationTokenSource();
            Port = port;
            Error = null;
            _ = AcceptLoopAsync(listener, _cts.Token);
            Log.Info($"Hook listener on http://127.0.0.1:{port}/hook");
        }
        catch (SocketException ex)
        {
            _listener = null;
            Port = port;
            Error = $"Port {port} is in use";
            Log.Error($"Hook listener could not bind port {port}", ex);
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        try { _listener?.Stop(); } catch (SocketException) { }
        _listener = null;
        Port = null;
        Error = null;
    }

    public void Dispose() => Stop();

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (SocketException ex)
            {
                Log.Error("Hook listener accept failed", ex);
                continue;
            }
            _ = HandleAsync(client, ct);
        }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(ReadTimeout);
                var stream = client.GetStream();
                var buffer = new byte[MaxBodyBytes + 8192];
                var total = 0;
                var headerEnd = -1;
                while (headerEnd < 0 && total < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(total), timeout.Token);
                    if (read == 0) break;
                    total += read;
                    headerEnd = IndexOfHeaderEnd(buffer, total);
                }
                if (headerEnd < 0)
                {
                    await RespondAsync(stream, "400 Bad Request", "", timeout.Token);
                    return;
                }

                var lines = Encoding.ASCII.GetString(buffer, 0, headerEnd).Split("\r\n");
                var request = lines[0].Split(' ');
                if (request.Length < 2)
                {
                    await RespondAsync(stream, "400 Bad Request", "", timeout.Token);
                    return;
                }
                var method = request[0];
                var path = request[1];
                var contentLength = 0;
                foreach (var line in lines.Skip(1))
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        _ = int.TryParse(line["Content-Length:".Length..].Trim(), out contentLength);
                }
                if (contentLength > MaxBodyBytes)
                {
                    await RespondAsync(stream, "413 Payload Too Large", "", timeout.Token);
                    return;
                }

                var bodyStart = headerEnd + 4;
                while (total - bodyStart < contentLength)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(total), timeout.Token);
                    if (read == 0) break;
                    total += read;
                }
                var bodyLength = Math.Max(0, Math.Min(contentLength, total - bodyStart));
                var body = Encoding.UTF8.GetString(buffer, bodyStart, bodyLength);

                if (method == "POST" && path == "/hook")
                {
                    HookReceived?.Invoke(body);
                    await RespondAsync(stream, "200 OK", "", timeout.Token);
                }
                else if (method == "GET" && path == "/health")
                {
                    await RespondAsync(stream, "200 OK", "ClaudeToolbar " + AppVersion(), timeout.Token);
                }
                else
                {
                    await RespondAsync(stream, "404 Not Found", "", timeout.Token);
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or SocketException or ObjectDisposedException)
            {
                // Slow or aborted clients are expected; nothing to do.
            }
            catch (Exception ex)
            {
                Log.Error("Hook request failed", ex);
            }
        }
    }

    private static async Task RespondAsync(NetworkStream stream, string status, string body, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var header = $"HTTP/1.1 {status}\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(header), ct);
        if (bytes.Length > 0) await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
    }

    private static int IndexOfHeaderEnd(byte[] buffer, int length)
    {
        for (var i = 3; i < length; i++)
        {
            if (buffer[i - 3] == '\r' && buffer[i - 2] == '\n' && buffer[i - 1] == '\r' && buffer[i] == '\n')
                return i - 3;
        }
        return -1;
    }

    private static string AppVersion() => typeof(HookListener).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
}
