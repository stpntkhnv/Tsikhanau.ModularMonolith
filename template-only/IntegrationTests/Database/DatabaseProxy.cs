using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Npgsql;
using Tsikhanau.Monolith.Shared.Collections;

namespace Tsikhanau.Monolith.IntegrationTests.Database;

internal sealed class DatabaseProxy : IAsyncDisposable
{
    private const Int32 BufferSize = 8192;

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stoppingSource = new();
    private readonly ConcurrentBag<TcpClient> _connections = [];
    private readonly ConcurrentBag<Task> _forwardings = [];
    private readonly Lock _answeringLock = new();
    private readonly List<TcpClient> _heldClients = [];
    private readonly String _serverHost;
    private readonly Int32 _serverPort;
    private readonly Task _accepting;
    private volatile Boolean _isAnswering = true;
    private Int32 _heldConnectionCount;
    private Int32 _openSilentConnectionCount;
    private Int32 _answerableConnectionCount = Int32.MaxValue;
    private Boolean _discardsSilentConnections;

    private DatabaseProxy(NpgsqlConnectionStringBuilder serverConnectionStringBuilder)
    {
        _serverHost = serverConnectionStringBuilder.Host!;
        _serverPort = serverConnectionStringBuilder.Port;
        _listener.Start();
        ConnectionString = new NpgsqlConnectionStringBuilder(serverConnectionStringBuilder.ConnectionString)
        {
            Host = IPAddress.Loopback.ToString(),
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port,
        }.ConnectionString;

        _accepting = AcceptAsync();
    }

    public String ConnectionString { get; }

    public Int32 HeldConnectionCount => Volatile.Read(ref _heldConnectionCount);

    public Int32 OpenSilentConnectionCount => Volatile.Read(ref _openSilentConnectionCount);

    public static DatabaseProxy Start(String serverConnectionString) =>
        new(new NpgsqlConnectionStringBuilder(serverConnectionString));

    public void AnswerOnlyNext(Int32 connectionCount)
    {
        lock (_answeringLock)
        {
            _answerableConnectionCount = connectionCount;
            _discardsSilentConnections = true;
        }
    }

    public void StopAnswering()
    {
        lock (_answeringLock)
        {
            _isAnswering = false;
        }
    }

    public void StartAnswering()
    {
        lock (_answeringLock)
        {
            _isAnswering = true;
            _heldClients.ForEach(Forward);
            _heldClients.Clear();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stoppingSource.CancelAsync();
        _listener.Stop();
        _connections.ForEach(connection => connection.Dispose());
        await _accepting;
        await Task.WhenAll(_forwardings);
        _stoppingSource.Dispose();
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync(_stoppingSource.Token);
                _connections.Add(client);
                Admit(client);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Admit(TcpClient client)
    {
        lock (_answeringLock)
        {
            if (_isAnswering && _answerableConnectionCount > 0)
            {
                _answerableConnectionCount--;
                Forward(client);
                return;
            }

            Interlocked.Increment(ref _heldConnectionCount);
            if (_discardsSilentConnections)
            {
                Interlocked.Increment(ref _openSilentConnectionCount);
                _forwardings.Add(DiscardAsync(client));
                return;
            }

            _heldClients.Add(client);
        }
    }

    private async Task DiscardAsync(TcpClient client)
    {
        var buffer = new Byte[BufferSize];
        try
        {
            var stream = client.GetStream();
            while (await stream.ReadAsync(buffer, _stoppingSource.Token) > 0)
            {
            }
        }
        catch (Exception exception) when (IsConnectionEnd(exception))
        {
        }
        finally
        {
            Interlocked.Decrement(ref _openSilentConnectionCount);
        }
    }

    private void Forward(TcpClient client) => _forwardings.Add(ForwardAsync(client));

    private async Task ForwardAsync(TcpClient client)
    {
        var server = new TcpClient();
        _connections.Add(server);
        try
        {
            await server.ConnectAsync(_serverHost, _serverPort, _stoppingSource.Token);
            await Task.WhenAll(
                CopyAsync(client.GetStream(), server.GetStream()),
                CopyAsync(server.GetStream(), client.GetStream()));
        }
        catch (Exception exception) when (IsConnectionEnd(exception))
        {
        }
    }

    private async Task CopyAsync(NetworkStream source, NetworkStream destination)
    {
        var buffer = new Byte[BufferSize];
        var readCount = await source.ReadAsync(buffer, _stoppingSource.Token);
        while (readCount > 0)
        {
            if (_isAnswering)
            {
                await destination.WriteAsync(buffer.AsMemory(0, readCount), _stoppingSource.Token);
            }

            readCount = await source.ReadAsync(buffer, _stoppingSource.Token);
        }

        destination.Socket.Shutdown(SocketShutdown.Send);
    }

    private static Boolean IsConnectionEnd(Exception exception) =>
        exception is IOException or SocketException or ObjectDisposedException or OperationCanceledException;
}
