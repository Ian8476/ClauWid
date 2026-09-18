using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ClaudeUsageWidget.Tests.Support;

/// <summary>
/// Minimal HTTP/1.1 server on loopback: answers each connection with the next queued response
/// and counts the requests that actually reached it.
/// </summary>
internal sealed class StubUsageServer : IDisposable
{
    private const string NoResponseQueued = "HTTP/1.1 599 No response queued\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly Queue<string> _responses = new();
    private int _requestCount;
    private string _lastRequest = string.Empty;

    public StubUsageServer()
    {
        _listener.Start();
        BaseAddress = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
        _ = ServeAsync();
    }

    public Uri BaseAddress { get; }

    public int RequestCount => Volatile.Read(ref _requestCount);

    public string LastRequest => Volatile.Read(ref _lastRequest);

    public void Respond(HttpStatusCode status, string body, params string[] headers)
    {
        var response = new StringBuilder()
            .Append($"HTTP/1.1 {(int)status} {status}\r\n")
            .Append("Content-Type: application/json\r\n")
            .Append($"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n")
            .Append("Connection: close\r\n");

        foreach (var header in headers)
        {
            response.Append(header).Append("\r\n");
        }

        response.Append("\r\n").Append(body);

        lock (_responses)
        {
            _responses.Enqueue(response.ToString());
        }
    }

    public void Dispose() => _listener.Stop();

    private async Task ServeAsync()
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync();
            }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
            {
                return;
            }

            using (client)
            {
                var stream = client.GetStream();
                var request = await ReadHeadersAsync(stream);

                Volatile.Write(ref _lastRequest, request);
                Interlocked.Increment(ref _requestCount);

                string response;
                lock (_responses)
                {
                    response = _responses.Count > 0 ? _responses.Dequeue() : NoResponseQueued;
                }

                await stream.WriteAsync(Encoding.UTF8.GetBytes(response));
            }
        }
    }

    private static async Task<string> ReadHeadersAsync(NetworkStream stream)
    {
        var buffer = new byte[4096];
        var request = new StringBuilder();

        while (!request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0)
            {
                break;
            }

            request.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        return request.ToString();
    }
}
