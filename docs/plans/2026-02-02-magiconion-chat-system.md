# MagicOnion Production-Ready Chat System Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a production-ready real-time chat system using MagicOnion (gRPC) with Redis backplane for horizontal scaling, comprehensive logging, health checks, and graceful shutdown.

**Architecture:** Server uses MagicOnion streaming hub for real-time bidirectional communication. Redis pub/sub acts as backplane for multi-instance message distribution. Client connects via gRPC channel and implements streaming receiver for messages. Server periodically broadcasts automated notifications to demonstrate server-initiated messaging.

**Tech Stack:** .NET 10, MagicOnion 7.x, MessagePack, Redis (StackExchange.Redis), Serilog, ASP.NET Core Health Checks, Docker Compose

---

## Task 1: Add NuGet Packages to Server Project

**Files:**
- Modify: `Chat.Server/Chat.Server.csproj:11-13`

**Step 1: Add MagicOnion and dependencies to server**

```xml
<ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.2"/>
    <PackageReference Include="MagicOnion.Server" Version="7.0.3" />
    <PackageReference Include="MagicOnion.Server.Redis" Version="7.0.3" />
    <PackageReference Include="Grpc.AspNetCore" Version="2.71.0" />
    <PackageReference Include="Grpc.AspNetCore.Server.Reflection" Version="2.71.0" />
    <PackageReference Include="StackExchange.Redis" Version="2.8.16" />
    <PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />
    <PackageReference Include="Serilog.Enrichers.Environment" Version="3.1.0" />
    <PackageReference Include="Serilog.Enrichers.Thread" Version="4.0.0" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
    <PackageReference Include="AspNetCore.HealthChecks.Redis" Version="9.0.1" />
    <PackageReference Include="AspNetCore.HealthChecks.Uris" Version="9.0.1" />
</ItemGroup>
```

**Step 2: Verify package restore**

Run: `dotnet restore Chat.Server`
Expected: "Restore succeeded"

**Step 3: Commit**

```bash
git add Chat.Server/Chat.Server.csproj
git commit -m "feat(server): add MagicOnion and production dependencies"
```

---

## Task 2: Add NuGet Packages to Client Project

**Files:**
- Modify: `Chat.Client/Chat.Client.csproj:9-15`

**Step 1: Add MagicOnion client packages**

```xml
<ItemGroup>
  <Content Include="..\.dockerignore">
    <Link>.dockerignore</Link>
  </Content>
</ItemGroup>

<ItemGroup>
    <PackageReference Include="MagicOnion.Client" Version="7.0.3" />
    <PackageReference Include="Grpc.Net.Client" Version="2.71.0" />
    <PackageReference Include="MessagePack" Version="3.0.238" />
</ItemGroup>
```

**Step 2: Verify package restore**

Run: `dotnet restore Chat.Client`
Expected: "Restore succeeded"

**Step 3: Commit**

```bash
git add Chat.Client/Chat.Client.csproj
git commit -m "feat(client): add MagicOnion client dependencies"
```

---

## Task 3: Create Shared Contracts Project

**Files:**
- Create: `Chat.Contracts/Chat.Contracts.csproj`
- Modify: `Chat.slnx:6-7`

**Step 1: Create new classlib project**

Run: `dotnet new classlib -n Chat.Contracts -f net10.0 -o Chat.Contracts`
Expected: "The template "Class Library" was created successfully"

**Step 2: Delete default Class1.cs**

Run: `rm Chat.Contracts/Class1.cs`
Expected: File removed

**Step 3: Add MagicOnion shared package**

Modify `Chat.Contracts/Chat.Contracts.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="MagicOnion.Abstractions" Version="7.0.3" />
        <PackageReference Include="MessagePack.Annotations" Version="3.0.238" />
    </ItemGroup>

</Project>
```

**Step 4: Add project references**

Run commands:
```bash
dotnet add Chat.Server reference Chat.Contracts
dotnet add Chat.Client reference Chat.Contracts
```
Expected: "Reference added"

**Step 5: Update solution file**

Modify `Chat.slnx`:
```xml
<Solution>
  <Folder Name="/Solution Items/">
    <File Path="compose.yaml" />
  </Folder>
  <Project Path="Chat.Contracts/Chat.Contracts.csproj" />
  <Project Path="Chat.Client/Chat.Client.csproj" />
  <Project Path="Chat.Server/Chat.Server.csproj" />
</Solution>
```

**Step 6: Build solution to verify**

Run: `dotnet build`
Expected: "Build succeeded"

**Step 7: Commit**

```bash
git add Chat.Contracts/ Chat.Server/Chat.Server.csproj Chat.Client/Chat.Client.csproj Chat.slnx
git commit -m "feat: add shared contracts project"
```

---

## Task 4: Create Chat Contracts

**Files:**
- Create: `Chat.Contracts/IChatHub.cs`
- Create: `Chat.Contracts/IChatHubReceiver.cs`
- Create: `Chat.Contracts/MessageData.cs`

**Step 1: Create message data model**

Create `Chat.Contracts/MessageData.cs`:

```csharp
using MessagePack;

namespace Chat.Contracts;

/// <summary>
/// Represents a chat message sent between client and server.
/// </summary>
[MessagePackObject]
public sealed class MessageData
{
    /// <summary>
    /// Username of the sender. Empty for server-initiated messages.
    /// </summary>
    [Key(0)]
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// The message content.
    /// </summary>
    [Key(1)]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// When true, indicates this message originated from the server.
    /// </summary>
    [Key(2)]
    public bool IsServerMessage { get; init; }

    /// <summary>
    /// UTC timestamp when the message was created.
    /// </summary>
    [Key(3)]
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}
```

**Step 2: Create client receiver interface**

Create `Chat.Contracts/IChatHubReceiver.cs`:

```csharp
namespace Chat.Contracts;

/// <summary>
/// Client-side receiver interface for receiving messages from the server.
/// </summary>
public interface IChatHubReceiver
{
    /// <summary>
    /// Called when a new message is broadcast to all clients.
    /// </summary>
    /// <param name="message">The message data.</param>
    void OnReceiveMessage(MessageData message);
}
```

**Step 3: Create hub interface**

Create `Chat.Contracts/IChatHub.cs`:

```csharp
using MagicOnion;

namespace Chat.Contracts;

/// <summary>
/// MagicOnion StreamingHub interface for real-time chat communication.
/// </summary>
public interface IChatHub : IStreamingHub<IChatHub, IChatHubReceiver>
{
    /// <summary>
    /// Joins the chat with the specified username.
    /// </summary>
    /// <param name="username">The username to join with.</param>
    Task JoinAsync(string username);

    /// <summary>
    /// Leaves the chat and disconnects.
    /// </summary>
    Task LeaveAsync();

    /// <summary>
    /// Sends a message to all connected clients.
    /// </summary>
    /// <param name="message">The message text to send.</param>
    Task SendMessageAsync(string message);
}
```

**Step 4: Build contracts project**

Run: `dotnet build Chat.Contracts`
Expected: "Build succeeded"

**Step 5: Commit**

```bash
git add Chat.Contracts/
git commit -m "feat(contracts): add chat hub interfaces and message model"
```

---

## Task 5: Configure Server Application Settings

**Files:**
- Modify: `Chat.Server/appsettings.json`
- Create: `Chat.Server/appsettings.Development.json`

**Step 1: Update appsettings.json**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Grpc": "Information",
      "MagicOnion": "Information"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "EndpointDefaults": {
      "Protocols": "Http2"
    }
  },
  "Redis": {
    "ConnectionString": "redis:6379,abortConnect=false,connectRetry=3,connectTimeout=5000"
  },
  "Chat": {
    "ServerNotificationIntervalSeconds": 30,
    "EnableServerNotifications": true
  }
}
```

**Step 2: Update appsettings.Development.json**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "System": "Information",
      "Microsoft": "Information",
      "Grpc": "Debug",
      "MagicOnion": "Debug"
    }
  },
  "Redis": {
    "ConnectionString": "localhost:6379,abortConnect=false,connectRetry=3,connectTimeout=5000"
  }
}
```

**Step 3: Build server to verify**

Run: `dotnet build Chat.Server`
Expected: "Build succeeded"

**Step 4: Commit**

```bash
git add Chat.Server/appsettings*.json
git commit -m "feat(server): configure application settings for MagicOnion and Redis"
```

---

## Task 6: Implement ChatHub Server

**Files:**
- Create: `Chat.Server/Hubs/ChatHub.cs`
- Create: `Chat.Server/Services/IRedisMessageBus.cs`

**Step 1: Create hub directory**

Run: `mkdir -p Chat.Server/Hubs`
Expected: Directory created

**Step 2: Create Redis message bus interface**

Create `Chat.Server/Services/IRedisMessageBus.cs`:

```csharp
using Chat.Contracts;

namespace Chat.Server.Services;

/// <summary>
/// Abstracts Redis pub/sub for distributing messages across server instances.
/// </summary>
public interface IRedisMessageBus
{
    /// <summary>
    /// Publishes a message to all server instances.
    /// </summary>
    Task PublishMessageAsync(MessageData message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to messages from all server instances.
    /// </summary>
    /// <param name="onMessage">Callback invoked when a message is received.</param>
    Task SubscribeAsync(Func<MessageData, Task> onMessage, CancellationToken cancellationToken = default);
}
```

**Step 3: Create ChatHub implementation**

Create `Chat.Server/Hubs/ChatHub.cs`:

```csharp
using Chat.Contracts;
using Chat.Server.Services;
using MagicOnion.Server.Hubs;
using MessagePack;

namespace Chat.Server.Hubs;

/// <summary>
/// MagicOnion StreamingHub implementation for real-time chat.
/// Thread-safe: MagicOnion ensures single-threaded execution per client connection.
/// </summary>
public sealed class ChatHub : StreamingHubBase<IChatHub, IChatHubReceiver>, IChatHub
{
    private readonly ILogger<ChatHub> _logger;
    private readonly IRedisMessageBus _messageBus;
    private readonly IGroup _broadcastGroup;

    private string _username = string.Empty;
    private bool _isJoined;

    public ChatHub(
        ILogger<ChatHub> logger,
        IRedisMessageBus messageBus)
    {
        _logger = logger;
        _messageBus = messageBus;
        _broadcastGroup = Group.AddAsync("Global").GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task JoinAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning("Client attempted to join with empty username");
            throw new ArgumentException("Username cannot be empty", nameof(username));
        }

        if (_isJoined)
        {
            _logger.LogWarning("Client {Username} attempted to join twice", _username);
            return;
        }

        _username = username;
        _isJoined = true;

        _logger.LogInformation("User {Username} joined the chat (ConnectionId: {ConnectionId})",
            _username, Context.ConnectionId);

        // Publish join notification to all instances
        var joinMessage = new MessageData
        {
            Username = "System",
            Message = $"{_username} joined the chat",
            IsServerMessage = true,
            TimestampUtc = DateTime.UtcNow
        };

        await _messageBus.PublishMessageAsync(joinMessage, Context.CallCancelled);
    }

    /// <inheritdoc />
    public async Task LeaveAsync()
    {
        if (!_isJoined)
        {
            return;
        }

        _logger.LogInformation("User {Username} left the chat", _username);

        // Publish leave notification to all instances
        var leaveMessage = new MessageData
        {
            Username = "System",
            Message = $"{_username} left the chat",
            IsServerMessage = true,
            TimestampUtc = DateTime.UtcNow
        };

        await _messageBus.PublishMessageAsync(leaveMessage, Context.CallCancelled);

        _isJoined = false;
    }

    /// <inheritdoc />
    public async Task SendMessageAsync(string message)
    {
        if (!_isJoined)
        {
            _logger.LogWarning("Client attempted to send message without joining");
            throw new InvalidOperationException("Must join chat before sending messages");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogDebug("User {Username} sent empty message", _username);
            return;
        }

        var messageData = new MessageData
        {
            Username = _username,
            Message = message,
            IsServerMessage = false,
            TimestampUtc = DateTime.UtcNow
        };

        _logger.LogDebug("User {Username} sending message: {Message}", _username, message);

        // Publish to Redis for distribution across all server instances
        await _messageBus.PublishMessageAsync(messageData, Context.CallCancelled);
    }

    protected override async ValueTask OnDisconnected()
    {
        if (_isJoined)
        {
            await LeaveAsync();
        }

        await base.OnDisconnected();
    }
}
```

**Step 4: Build server**

Run: `dotnet build Chat.Server`
Expected: "Build succeeded"

**Step 5: Commit**

```bash
git add Chat.Server/Hubs/ Chat.Server/Services/
git commit -m "feat(server): implement ChatHub with Redis message bus abstraction"
```

---

## Task 7: Implement Redis Message Bus

**Files:**
- Create: `Chat.Server/Services/RedisMessageBus.cs`

**Step 1: Create Redis message bus implementation**

Create `Chat.Server/Services/RedisMessageBus.cs`:

```csharp
using System.Threading.Channels;
using Chat.Contracts;
using MessagePack;
using StackExchange.Redis;

namespace Chat.Server.Services;

/// <summary>
/// Redis-based message bus for distributing chat messages across server instances.
/// Uses pub/sub pattern for horizontal scalability.
/// </summary>
public sealed class RedisMessageBus : IRedisMessageBus, IAsyncDisposable
{
    private const string ChatChannel = "chat:messages";

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisMessageBus> _logger;
    private readonly Channel<MessageData> _localMessageQueue;
    private readonly CancellationTokenSource _disposeCts;

    private Func<MessageData, Task>? _messageHandler;
    private Task? _subscriptionTask;

    public RedisMessageBus(
        IConnectionMultiplexer redis,
        ILogger<RedisMessageBus> logger)
    {
        _redis = redis;
        _logger = logger;
        _disposeCts = new CancellationTokenSource();

        // Bounded channel to prevent memory issues under load
        _localMessageQueue = Channel.CreateBounded<MessageData>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    /// <inheritdoc />
    public async Task PublishMessageAsync(MessageData message, CancellationToken cancellationToken = default)
    {
        try
        {
            var subscriber = _redis.GetSubscriber();
            var serialized = MessagePackSerializer.Serialize(message);

            await subscriber.PublishAsync(
                RedisChannel.Literal(ChatChannel),
                serialized,
                CommandFlags.FireAndForget);

            _logger.LogDebug("Published message from {Username} to Redis", message.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to Redis");

            // Fallback: deliver locally even if Redis fails
            await _localMessageQueue.Writer.WriteAsync(message, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task SubscribeAsync(Func<MessageData, Task> onMessage, CancellationToken cancellationToken = default)
    {
        if (_messageHandler is not null)
        {
            throw new InvalidOperationException("Already subscribed");
        }

        _messageHandler = onMessage;

        var subscriber = _redis.GetSubscriber();

        await subscriber.SubscribeAsync(
            RedisChannel.Literal(ChatChannel),
            async (channel, value) =>
            {
                try
                {
                    var message = MessagePackSerializer.Deserialize<MessageData>(value!);
                    await _localMessageQueue.Writer.WriteAsync(message, _disposeCts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize message from Redis");
                }
            });

        _logger.LogInformation("Subscribed to Redis channel: {Channel}", ChatChannel);

        // Start background processing of messages
        _subscriptionTask = ProcessMessagesAsync(_disposeCts.Token);
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        await foreach (var message in _localMessageQueue.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                if (_messageHandler is not null)
                {
                    await _messageHandler(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from {Username}", message.Username);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposeCts.Cancel();
        _localMessageQueue.Writer.Complete();

        if (_subscriptionTask is not null)
        {
            try
            {
                await _subscriptionTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }

        try
        {
            var subscriber = _redis.GetSubscriber();
            await subscriber.UnsubscribeAsync(RedisChannel.Literal(ChatChannel));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error unsubscribing from Redis");
        }

        _disposeCts.Dispose();
    }
}
```

**Step 2: Build server**

Run: `dotnet build Chat.Server`
Expected: "Build succeeded"

**Step 3: Commit**

```bash
git add Chat.Server/Services/RedisMessageBus.cs
git commit -m "feat(server): implement Redis pub/sub message bus with failover"
```

---

## Task 8: Create Message Broadcaster Service

**Files:**
- Create: `Chat.Server/Services/MessageBroadcaster.cs`

**Step 1: Create broadcaster service**

Create `Chat.Server/Services/MessageBroadcaster.cs`:

```csharp
using Chat.Contracts;
using Chat.Server.Services;
using MagicOnion.Server.Hubs;

namespace Chat.Server.Services;

/// <summary>
/// Background service that receives messages from Redis and broadcasts to connected clients.
/// </summary>
public sealed class MessageBroadcaster : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageBroadcaster> _logger;

    public MessageBroadcaster(
        IServiceProvider serviceProvider,
        ILogger<MessageBroadcaster> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message broadcaster starting");

        // Wait a moment for the server to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

        using var scope = _serviceProvider.CreateScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IRedisMessageBus>();
        var groupRepository = scope.ServiceProvider.GetRequiredService<IGroupRepositoryFactory>();

        var repository = groupRepository.CreateRepository();

        await messageBus.SubscribeAsync(async message =>
        {
            try
            {
                var group = await repository.GetAsync("Global");
                if (group is not null)
                {
                    group.All.OnReceiveMessage(message);

                    _logger.LogDebug(
                        "Broadcast message from {Username} to {ClientCount} clients",
                        message.Username,
                        group.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast message");
            }
        }, stoppingToken);

        _logger.LogInformation("Message broadcaster running");

        // Keep service alive
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Message broadcaster stopping");
        }
    }
}
```

**Step 2: Build server**

Run: `dotnet build Chat.Server`
Expected: "Build succeeded"

**Step 3: Commit**

```bash
git add Chat.Server/Services/MessageBroadcaster.cs
git commit -m "feat(server): add message broadcaster for Redis-to-clients distribution"
```

---

## Task 9: Create Server Notification Service

**Files:**
- Create: `Chat.Server/Services/ServerNotificationService.cs`
- Create: `Chat.Server/Configuration/ChatOptions.cs`

**Step 1: Create configuration options**

Create `Chat.Server/Configuration/ChatOptions.cs`:

```csharp
namespace Chat.Server.Configuration;

/// <summary>
/// Configuration options for chat server behavior.
/// </summary>
public sealed class ChatOptions
{
    public const string SectionName = "Chat";

    /// <summary>
    /// Interval in seconds between server notifications.
    /// </summary>
    public int ServerNotificationIntervalSeconds { get; init; } = 30;

    /// <summary>
    /// Whether to send periodic server notifications.
    /// </summary>
    public bool EnableServerNotifications { get; init; } = true;
}
```

**Step 2: Create notification service**

Create `Chat.Server/Services/ServerNotificationService.cs`:

```csharp
using Chat.Contracts;
using Chat.Server.Configuration;
using Microsoft.Extensions.Options;

namespace Chat.Server.Services;

/// <summary>
/// Background service that periodically sends server notifications to all clients.
/// Demonstrates server-initiated messaging capability.
/// </summary>
public sealed class ServerNotificationService : BackgroundService
{
    private readonly IRedisMessageBus _messageBus;
    private readonly ILogger<ServerNotificationService> _logger;
    private readonly ChatOptions _options;

    public ServerNotificationService(
        IRedisMessageBus messageBus,
        ILogger<ServerNotificationService> logger,
        IOptions<ChatOptions> options)
    {
        _messageBus = messageBus;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableServerNotifications)
        {
            _logger.LogInformation("Server notifications disabled");
            return;
        }

        _logger.LogInformation(
            "Server notification service starting (interval: {Interval}s)",
            _options.ServerNotificationIntervalSeconds);

        // Wait for server to fully start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        var interval = TimeSpan.FromSeconds(_options.ServerNotificationIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                var notification = new MessageData
                {
                    Username = "SERVER",
                    Message = $"Server time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                    IsServerMessage = true,
                    TimestampUtc = DateTime.UtcNow
                };

                await _messageBus.PublishMessageAsync(notification, stoppingToken);

                _logger.LogDebug("Sent periodic server notification");
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending server notification");
            }
        }

        _logger.LogInformation("Server notification service stopped");
    }
}
```

**Step 3: Build server**

Run: `dotnet build Chat.Server`
Expected: "Build succeeded"

**Step 4: Commit**

```bash
git add Chat.Server/Configuration/ Chat.Server/Services/ServerNotificationService.cs
git commit -m "feat(server): add periodic server notification service"
```

---

## Task 10: Configure Server Program.cs with Production Features

**Files:**
- Modify: `Chat.Server/Program.cs`
- Delete: `Chat.Server/Worker.cs`

**Step 1: Replace Program.cs with full configuration**

```csharp
using Chat.Server.Configuration;
using Chat.Server.Hubs;
using Chat.Server.Services;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    // Bind configuration
    builder.Services.Configure<ChatOptions>(
        builder.Configuration.GetSection(ChatOptions.SectionName));

    // Redis connection
    var redisConnectionString = builder.Configuration["Redis:ConnectionString"]
        ?? throw new InvalidOperationException("Redis connection string not configured");

    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        var options = ConfigurationOptions.Parse(redisConnectionString);
        options.AbortOnConnectFail = false;
        options.ReconnectRetryPolicy = new ExponentialRetry(5000);
        return ConnectionMultiplexer.Connect(options);
    });

    // Redis message bus
    builder.Services.AddSingleton<IRedisMessageBus, RedisMessageBus>();

    // MagicOnion gRPC services
    builder.Services.AddGrpc(options =>
    {
        options.MaxReceiveMessageSize = 4 * 1024 * 1024; // 4 MB
        options.MaxSendMessageSize = 4 * 1024 * 1024;
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    });

    builder.Services.AddMagicOnion();

    // Background services
    builder.Services.AddHostedService<MessageBroadcaster>();
    builder.Services.AddHostedService<ServerNotificationService>();

    // Health checks
    builder.Services
        .AddHealthChecks()
        .AddRedis(
            redisConnectionString,
            name: "redis",
            tags: ["ready"]);

    var app = builder.Build();

    // Map endpoints
    app.MapMagicOnionService();

    // gRPC reflection for development
    if (app.Environment.IsDevelopment())
    {
        app.MapGrpcReflectionService();
    }

    // Health check endpoints
    app.MapHealthChecks("/health/ready", new()
    {
        Predicate = check => check.Tags.Contains("ready")
    });

    app.MapHealthChecks("/health/live", new()
    {
        Predicate = _ => false // No checks = liveness
    });

    Log.Information("Starting Chat Server on {Environment}", app.Environment.EnvironmentName);
    Log.Information("Redis: {Redis}", redisConnectionString);

    await app.RunAsync();

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
```

**Step 2: Delete old Worker.cs**

Run: `rm Chat.Server/Worker.cs`
Expected: File removed

**Step 3: Build server**

Run: `dotnet build Chat.Server`
Expected: "Build succeeded"

**Step 4: Commit**

```bash
git add Chat.Server/Program.cs
git rm Chat.Server/Worker.cs
git commit -m "feat(server): configure production-ready server with Serilog, health checks, and graceful shutdown"
```

---

## Task 11: Implement Console Client

**Files:**
- Modify: `Chat.Client/Program.cs`

**Step 1: Implement full client with user interaction**

```csharp
using Chat.Contracts;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;

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

try
{
    // Create gRPC channel
    var channel = GrpcChannel.ForAddress(serverAddress, new GrpcChannelOptions
    {
        MaxReceiveMessageSize = 4 * 1024 * 1024,
        MaxSendMessageSize = 4 * 1024 * 1024,
        Credentials = ChannelCredentials.Insecure
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
    var cts = new CancellationTokenSource();
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

                await hub.SendMessageAsync(input);
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

/// <summary>
/// Receives and displays messages from the server.
/// </summary>
sealed class ChatReceiver : IChatHubReceiver
{
    public void OnReceiveMessage(MessageData message)
    {
        var timestamp = message.TimestampUtc.ToLocalTime().ToString("HH:mm:ss");

        if (message.IsServerMessage)
        {
            // Server messages with prefix
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{timestamp}] [SERVER]: {message.Message}");
            Console.ForegroundColor = color;
        }
        else
        {
            // Regular user messages
            Console.WriteLine($"[{timestamp}] {message.Username}: {message.Message}");
        }
    }
}
```

**Step 2: Build client**

Run: `dotnet build Chat.Client`
Expected: "Build succeeded"

**Step 3: Commit**

```bash
git add Chat.Client/Program.cs
git commit -m "feat(client): implement interactive console chat client"
```

---

## Task 12: Update Docker Compose with Redis

**Files:**
- Modify: `compose.yaml`

**Step 1: Update compose file with Redis and proper networking**

```yaml
services:
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 5
    networks:
      - chat-network

  chat.server:
    image: chat.server
    build:
      context: .
      dockerfile: Chat.Server/Dockerfile
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8080
      - Redis__ConnectionString=redis:6379,abortConnect=false
      - Chat__ServerNotificationIntervalSeconds=30
      - Chat__EnableServerNotifications=true
    depends_on:
      redis:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/health/ready || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 3
      start_period: 10s
    networks:
      - chat-network
    restart: unless-stopped

  chat.server.2:
    image: chat.server
    build:
      context: .
      dockerfile: Chat.Server/Dockerfile
    ports:
      - "5001:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8080
      - Redis__ConnectionString=redis:6379,abortConnect=false
      - Chat__ServerNotificationIntervalSeconds=30
      - Chat__EnableServerNotifications=false
    depends_on:
      redis:
        condition: service_healthy
      chat.server:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/health/ready || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 3
      start_period: 10s
    networks:
      - chat-network
    restart: unless-stopped

  chat.client:
    image: chat.client
    build:
      context: .
      dockerfile: Chat.Client/Dockerfile
    stdin_open: true
    tty: true
    depends_on:
      chat.server:
        condition: service_healthy
    networks:
      - chat-network

networks:
  chat-network:
    driver: bridge
```

**Step 2: Test compose file syntax**

Run: `docker compose config`
Expected: Parsed YAML output without errors

**Step 3: Commit**

```bash
git add compose.yaml
git commit -m "feat(docker): configure multi-instance setup with Redis backplane"
```

---

## Task 13: Create Server Dockerfile

**Files:**
- Create: `Chat.Server/Dockerfile`

**Step 1: Create optimized multi-stage Dockerfile**

Create `Chat.Server/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY ["Chat.Server/Chat.Server.csproj", "Chat.Server/"]
COPY ["Chat.Contracts/Chat.Contracts.csproj", "Chat.Contracts/"]

# Restore dependencies
RUN dotnet restore "Chat.Server/Chat.Server.csproj"

# Copy source code
COPY . .

WORKDIR "/src/Chat.Server"
RUN dotnet build "Chat.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Chat.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

EXPOSE 8080
EXPOSE 8081

COPY --from=publish /app/publish .

# Non-root user for security
USER $APP_UID

ENTRYPOINT ["dotnet", "Chat.Server.dll"]
```

**Step 2: Commit**

```bash
git add Chat.Server/Dockerfile
git commit -m "feat(docker): add production-ready server Dockerfile"
```

---

## Task 14: Create Client Dockerfile

**Files:**
- Create: `Chat.Client/Dockerfile`

**Step 1: Create client Dockerfile**

Create `Chat.Client/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY ["Chat.Client/Chat.Client.csproj", "Chat.Client/"]
COPY ["Chat.Contracts/Chat.Contracts.csproj", "Chat.Contracts/"]

# Restore dependencies
RUN dotnet restore "Chat.Client/Chat.Client.csproj"

# Copy source code
COPY . .

WORKDIR "/src/Chat.Client"
RUN dotnet build "Chat.Client.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Chat.Client.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Chat.Client.dll"]
```

**Step 2: Commit**

```bash
git add Chat.Client/Dockerfile
git commit -m "feat(docker): add client Dockerfile"
```

---

## Task 15: Create .dockerignore File

**Files:**
- Create: `.dockerignore`

**Step 1: Create .dockerignore**

Create `.dockerignore`:

```
**/.classpath
**/.dockerignore
**/.env
**/.git
**/.gitignore
**/.project
**/.settings
**/.toolstarget
**/.vs
**/.vscode
**/*.*proj.user
**/*.dbmdl
**/*.jfm
**/azds.yaml
**/bin
**/charts
**/docker-compose*
**/Dockerfile*
**/node_modules
**/npm-debug.log
**/obj
**/secrets.dev.yaml
**/values.dev.yaml
LICENSE
README.md
**/docs
**/.idea
```

**Step 2: Commit**

```bash
git add .dockerignore
git commit -m "chore(docker): add .dockerignore for optimized builds"
```

---

## Task 16: Create README Documentation

**Files:**
- Create: `README.md`

**Step 1: Create comprehensive README**

Create `README.md`:

```markdown
# MagicOnion Production-Ready Chat System

A scalable, production-ready real-time chat system built with .NET 10 and MagicOnion (gRPC).

## Features

- **Real-time bidirectional communication** using MagicOnion StreamingHub
- **Horizontal scalability** with Redis pub/sub backplane
- **Production-ready** with structured logging, health checks, and graceful shutdown
- **Server-initiated notifications** demonstrating push capability
- **Multi-instance support** - messages distributed across all server instances
- **Docker-ready** with Docker Compose orchestration

## Architecture

```
┌─────────────┐      ┌─────────────┐      ┌─────────────┐
│   Client    │◄────►│   Server    │◄────►│    Redis    │
│  (Console)  │ gRPC │ Instance 1  │ Pub  │  (Backplane)│
└─────────────┘      └─────────────┘ Sub  └─────────────┘
                             ▲               ▲
                             │               │
                             │ Pub/Sub       │
                             │               │
                     ┌─────────────┐         │
                     │   Server    │◄────────┘
                     │ Instance 2  │
                     └─────────────┘
```

## Tech Stack

- **.NET 10** - Latest .NET platform
- **MagicOnion 7.x** - gRPC-based real-time communication
- **Redis** - Message distribution backplane
- **Serilog** - Structured logging
- **MessagePack** - Efficient serialization
- **Docker & Docker Compose** - Containerization

## Quick Start

### Prerequisites

- .NET 10 SDK
- Docker & Docker Compose (for containerized setup)
- Redis (if running locally without Docker)

### Running with Docker Compose (Recommended)

```bash
# Build and start all services (Redis + 2 server instances)
docker compose up --build

# In separate terminals, connect clients to different instances
docker compose run --rm chat.client
```

### Running Locally

**Terminal 1 - Start Redis:**
```bash
docker run -p 6379:6379 redis:7-alpine
```

**Terminal 2 - Start Server:**
```bash
cd Chat.Server
dotnet run
```

**Terminal 3 - Start Client:**
```bash
cd Chat.Client
dotnet run
```

## Configuration

Server configuration in `Chat.Server/appsettings.json`:

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Chat": {
    "ServerNotificationIntervalSeconds": 30,
    "EnableServerNotifications": true
  }
}
```

## Health Checks

- **Liveness:** `http://localhost:5000/health/live`
- **Readiness:** `http://localhost:5000/health/ready` (checks Redis connectivity)

## Project Structure

```
Chat/
├── Chat.Contracts/          # Shared interfaces and models
│   ├── IChatHub.cs         # Hub interface
│   ├── IChatHubReceiver.cs # Client receiver interface
│   └── MessageData.cs      # Message model
├── Chat.Server/            # Server application
│   ├── Hubs/
│   │   └── ChatHub.cs      # Hub implementation
│   ├── Services/
│   │   ├── IRedisMessageBus.cs
│   │   ├── RedisMessageBus.cs
│   │   ├── MessageBroadcaster.cs
│   │   └── ServerNotificationService.cs
│   ├── Configuration/
│   │   └── ChatOptions.cs
│   └── Program.cs
├── Chat.Client/            # Console client
│   └── Program.cs
└── compose.yaml            # Docker Compose orchestration
```

## Testing Multi-Instance Setup

1. Start with Docker Compose: `docker compose up --build`
2. Connect client to instance 1: `docker compose run --rm chat.client` (enter `http://chat.server:8080`)
3. Connect another client to instance 2: `docker compose run --rm chat.client` (enter `http://chat.server.2:8080`)
4. Send messages from either client - both receive messages via Redis backplane

## Production Considerations

✅ **Implemented:**
- Structured logging with Serilog
- Health checks (liveness and readiness)
- Graceful shutdown handling
- Connection retry logic
- Bounded channels for backpressure
- Message serialization with MessagePack
- Multi-instance support via Redis
- Docker health checks

🔜 **Consider for production:**
- Authentication & authorization
- Message persistence (database)
- Rate limiting
- Message history
- User presence tracking
- TLS/SSL certificates
- Monitoring & metrics (Prometheus)
- Distributed tracing (OpenTelemetry)

## License

MIT
```

**Step 2: Commit**

```bash
git add README.md
git commit -m "docs: add comprehensive README with architecture and usage"
```

---

## Task 17: Build and Test System

**Files:**
- None (verification only)

**Step 1: Clean solution**

Run: `dotnet clean`
Expected: "Clean succeeded"

**Step 2: Build entire solution**

Run: `dotnet build`
Expected: "Build succeeded. 0 Warning(s). 0 Error(s)."

**Step 3: Test Docker Compose configuration**

Run: `docker compose config`
Expected: Valid YAML output

**Step 4: Build Docker images (optional, time-intensive)**

Run: `docker compose build`
Expected: All images built successfully

**Step 5: Commit**

```bash
git add -A
git commit -m "build: verify complete solution builds successfully"
```

---

## Verification Checklist

Before considering this complete, verify:

- [ ] Solution builds without errors: `dotnet build`
- [ ] All three projects reference correct dependencies
- [ ] Server has MagicOnion, Redis, Serilog packages
- [ ] Client has MagicOnion.Client packages
- [ ] Contracts project is referenced by both server and client
- [ ] Docker Compose includes Redis and multi-instance server setup
- [ ] Health check endpoints configured
- [ ] Server logs structured output with Serilog
- [ ] README documents architecture and usage

## Next Steps

After implementation:

1. **Manual Testing:**
   - Start server locally: `cd Chat.Server && dotnet run`
   - Start Redis: `docker run -p 6379:6379 redis:7-alpine`
   - Start client: `cd Chat.Client && dotnet run`
   - Test message exchange
   - Test server notifications every 30 seconds

2. **Docker Testing:**
   - `docker compose up --build`
   - Connect multiple clients to different server instances
   - Verify message distribution across instances

3. **Production Hardening (Optional):**
   - Add authentication
   - Add rate limiting
   - Add monitoring/metrics
   - Deploy to cloud (Azure/AWS/GCP)

---

**Implementation complete! This plan provides a fully production-ready chat system with MagicOnion, Redis backplane, and comprehensive operational features.**
