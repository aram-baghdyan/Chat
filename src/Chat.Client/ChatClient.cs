using Chat.Client.Configuration;
using Chat.Contracts;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;

namespace Chat.Client;

/// <summary>
/// Connection state for the chat client.
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting
}

/// <summary>
/// Manages the connection to the chat server with automatic reconnection.
/// </summary>
public sealed class ChatClient : IAsyncDisposable
{
    private readonly ClientOptions _options;
    private readonly IChatHubReceiver _receiver;
    private readonly string _serverAddress;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private GrpcChannel? _channel;
    private IChatHub? _hub;
    private string? _username;
    private int _reconnectAttempts;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public bool IsConnected => State == ConnectionState.Connected;
    public string? Username => _username;

    public event Action<ConnectionState>? StateChanged;
    public event Action<string>? OnError;

    public ChatClient(string serverAddress, IChatHubReceiver receiver, ClientOptions? options = null)
    {
        _serverAddress = serverAddress;
        _receiver = receiver;
        _options = options ?? new ClientOptions();
    }

    /// <summary>
    /// Connects to the server and joins the chat with the specified username.
    /// </summary>
    public async Task ConnectAsync(string username, CancellationToken ct = default)
    {
        await _connectionLock.WaitAsync(ct);
        try
        {
            _username = username;
            await ConnectInternalAsync(ct);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Connects with automatic retry on failure.
    /// </summary>
    public async Task ConnectWithRetryAsync(string username, CancellationToken ct = default)
    {
        _username = username;
        _reconnectAttempts = 0;

        while (!ct.IsCancellationRequested && _reconnectAttempts < _options.MaxReconnectAttempts)
        {
            try
            {
                await _connectionLock.WaitAsync(ct);
                try
                {
                    await ConnectInternalAsync(ct);
                    _reconnectAttempts = 0;
                    return;
                }
                finally
                {
                    _connectionLock.Release();
                }
            }
            catch (RpcException ex) when (!ct.IsCancellationRequested)
            {
                _reconnectAttempts++;
                var isReconnect = State == ConnectionState.Reconnecting;

                OnError?.Invoke($"Connection failed: {ex.Status.Detail}");

                if (_reconnectAttempts >= _options.MaxReconnectAttempts)
                {
                    SetState(ConnectionState.Disconnected);
                    throw new InvalidOperationException(
                        $"Failed to connect after {_reconnectAttempts} attempts", ex);
                }

                SetState(ConnectionState.Reconnecting);
                await Task.Delay(_options.ReconnectDelayMs, ct);
            }
        }
    }

    /// <summary>
    /// Sends a message to the chat.
    /// </summary>
    public async Task SendMessageAsync(string message, CancellationToken ct = default)
    {
        if (_hub is null || State != ConnectionState.Connected)
        {
            throw new InvalidOperationException("Not connected to server");
        }

        try
        {
            await _hub.SendMessageAsync(message);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.Cancelled)
        {
            OnError?.Invoke("Connection lost while sending message");
            _ = ReconnectAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Leaves the chat gracefully.
    /// </summary>
    public async Task LeaveAsync()
    {
        if (_hub is not null && State == ConnectionState.Connected)
        {
            try
            {
                await _hub.LeaveAsync();
            }
            catch (RpcException)
            {
                // Ignore errors during leave - connection may already be closed
            }
        }
    }

    /// <summary>
    /// Attempts to reconnect to the server.
    /// </summary>
    private async Task ReconnectAsync(CancellationToken ct)
    {
        if (_username is null) return;

        await _connectionLock.WaitAsync(ct);
        try
        {
            if (State == ConnectionState.Reconnecting) return;

            SetState(ConnectionState.Reconnecting);
            await CleanupConnectionAsync();
        }
        finally
        {
            _connectionLock.Release();
        }

        await ConnectWithRetryAsync(_username, ct);
    }

    private async Task ConnectInternalAsync(CancellationToken ct)
    {
        SetState(ConnectionState.Connecting);

        // Clean up any existing connection
        await CleanupConnectionAsync();

        // Create new channel
        _channel = GrpcChannel.ForAddress(_serverAddress, new GrpcChannelOptions
        {
            MaxReceiveMessageSize = _options.MaxMessageSize,
            MaxSendMessageSize = _options.MaxMessageSize
        });

        // Connect to hub
        _hub = await StreamingHubClient.ConnectAsync<IChatHub, IChatHubReceiver>(
            _channel,
            _receiver,
            cancellationToken: ct);

        // Join chat
        await _hub.JoinAsync(_username!);

        SetState(ConnectionState.Connected);
    }

    private async Task CleanupConnectionAsync()
    {
        if (_hub is not null)
        {
            try
            {
                await _hub.DisposeAsync();
            }
            catch
            {
                // Ignore disposal errors
            }
            _hub = null;
        }

        if (_channel is not null)
        {
            try
            {
                await _channel.ShutdownAsync();
                _channel.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
            _channel = null;
        }
    }

    private void SetState(ConnectionState newState)
    {
        if (State == newState) return;
        State = newState;
        StateChanged?.Invoke(newState);
    }

    public async ValueTask DisposeAsync()
    {
        await LeaveAsync();
        await CleanupConnectionAsync();
        _connectionLock.Dispose();
        SetState(ConnectionState.Disconnected);
    }
}