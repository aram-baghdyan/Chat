using Chat.Client;
using Chat.Client.Configuration;
using Grpc.Core;

// Allow HTTP/2 without TLS (required for gRPC without HTTPS)
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var options = new ClientOptions();
var cts = new CancellationTokenSource();

// Handle Ctrl+C gracefully
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

ConsoleUI.WriteHeader();

// Get server address
var serverAddress = ConsoleUI.PromptForInput("Enter server address", options.DefaultServerAddress)
    ?? options.DefaultServerAddress;

// Ensure http:// prefix
if (!serverAddress.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
    !serverAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
{
    serverAddress = $"http://{serverAddress}";
}

// Get username
var username = ConsoleUI.PromptForRequiredInput("Enter your username");

Console.WriteLine();
ConsoleUI.WriteInfo($"Connecting to {serverAddress} as {username}...");

// Create receiver and client
var receiver = new ChatReceiver();
await using var client = new ChatClient(serverAddress, receiver, options);

// Subscribe to events
client.StateChanged += ConsoleUI.WriteConnectionState;
client.OnError += ConsoleUI.WriteError;

try
{
    // Connect with retry
    await client.ConnectWithRetryAsync(username, cts.Token);

    // Fetch and display chat history
    var history = await client.GetHistoryAsync(cts.Token);
    if (history.Length > 0)
    {
        ConsoleUI.WriteInfo($"--- Last {history.Length} messages ---");
        foreach (var msg in history)
        {
            receiver.OnReceiveMessage(msg);
        }
        ConsoleUI.WriteSeparator();
    }

    ConsoleUI.WriteSuccess("Connected! Type your messages (or 'exit' to quit):");
    ConsoleUI.WriteSeparator();

    // Message input loop
    await RunMessageLoopAsync(client, options, cts.Token);

    ConsoleUI.WriteInfo("Leaving chat...");
}
catch (OperationCanceledException)
{
    ConsoleUI.WriteInfo("Disconnecting...");
}
catch (InvalidOperationException ex)
{
    ConsoleUI.WriteError(ex.Message);
}
catch (RpcException ex)
{
    ConsoleUI.WriteError($"Connection error: {ex.Status.Detail}");
    ConsoleUI.WriteInfo("Make sure the server is running and accessible.");
}
catch (Exception ex)
{
    ConsoleUI.WriteError($"Unexpected error: {ex.Message}");
}

ConsoleUI.WriteInfo("Press any key to exit...");
Console.ReadKey(intercept: true);

return;

static async Task RunMessageLoopAsync(ChatClient client, ClientOptions options, CancellationToken ct)
{
    while (!ct.IsCancellationRequested && client.IsConnected)
    {
        try
        {
            // Non-blocking input check
            if (!Console.KeyAvailable)
            {
                await Task.Delay(50, ct);
                continue;
            }

            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (input.Equals(options.ExitCommand, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (!client.IsConnected)
            {
                ConsoleUI.WriteWarning("Not connected. Message not sent.");
                continue;
            }

            await client.SendMessageAsync(input, ct);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            ConsoleUI.WriteWarning("Connection lost. Attempting to reconnect...");
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteError($"Error sending message: {ex.Message}");
        }
    }
}