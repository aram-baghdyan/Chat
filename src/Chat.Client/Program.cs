using Chat.Contracts;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;

// Allow HTTP/2 without TLS (required for gRPC without HTTPS)
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

Console.WriteLine("=== MagicOnion Chat Client ===");
Console.WriteLine();

// Get server address
Console.Write("Enter server address (press Enter for localhost:5000): ");
var serverAddress = Console.ReadLine();
if (string.IsNullOrWhiteSpace(serverAddress))
{
    serverAddress = "http://localhost:5000";
}

// Get username
Console.Write("Enter your username: ");
var username = Console.ReadLine();
while (string.IsNullOrWhiteSpace(username))
{
    Console.WriteLine("Username cannot be empty!");
    Console.Write("Enter your username: ");
    username = Console.ReadLine();
}

Console.WriteLine();
Console.WriteLine($"Connecting to {serverAddress} as {username}...");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += async (_, e) =>                                                                                                                                                                
{                                                                                                                                                                                                        
    e.Cancel = true;                                                                                                                                                                                     
    await cts.CancelAsync();                                                                                                                                                                             
};  

try
{
    // Create gRPC channel (HTTP/2 without TLS)
    var channel = GrpcChannel.ForAddress(serverAddress, new GrpcChannelOptions
    {
        MaxReceiveMessageSize = 4 * 1024 * 1024,
        MaxSendMessageSize = 4 * 1024 * 1024
    });

    // Create receiver
    var receiver = new ChatReceiver();
    
    // Connect to hub
    var hub = await StreamingHubClient.ConnectAsync<IChatHub, IChatHubReceiver>(
        channel,
        receiver);

    Console.WriteLine("Connected! Type your messages (or 'exit' to quit):");
    Console.WriteLine(new string('-', 60));
    
    // Join chat
    await hub.JoinAsync(username);

    // Start message reading loop
    var readTask = Task.Run(async () =>
    {
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var input = Console.ReadLine();

                if (string.IsNullOrEmpty(input))
                {
                    continue;
                }

                if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    await cts.CancelAsync();
                    break;
                }

                hub.SendMessageAsync(input);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }
    }, cts.Token);

    // Wait for exit
    await readTask;

    // Leave chat
    await hub.LeaveAsync();
    await hub.DisposeAsync();
    await channel.ShutdownAsync();

    Console.WriteLine("Disconnected. Press any key to exit...");
    Console.ReadKey();
}
catch (RpcException ex)
{
    Console.WriteLine($"Connection error: {ex.Status.Detail}");
    Console.WriteLine("Make sure the server is running and accessible.");
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error: {ex.Message}");
}