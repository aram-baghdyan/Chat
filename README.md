# MagicOnion Production-Ready Chat System

A scalable, production-ready real-time chat system built with .NET 10 and MagicOnion (gRPC).

## Features

- **Real-time bidirectional communication** using MagicOnion StreamingHub
- **Horizontal scalability** with Redis pub/sub backplane
- **Production-ready** with structured logging, health checks, and graceful shutdown
- **Server-initiated notifications** demonstrating push capability
- **Multi-instance support** - messages distributed across all server instances
- **Chat history** — last N messages replayed on connect, backed by Redis
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
- **OpenTelemetry** - Metrics and distributed tracing
- **NGINX** - gRPC reverse proxy with rate limiting
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
cd src/Chat.Server
dotnet run
```

**Terminal 3 - Start Client:**
```bash
cd src/Chat.Client
dotnet run
```

## Configuration

Server configuration in `src/Chat.Server/appsettings.json`:

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Chat": {
    "ServerNotificationIntervalSeconds": 30,
    "EnableServerNotifications": true,
    "ServerAddress": "http://localhost:8080",
    "MaxUsernameLength": 50,
    "MaxMessageLength": 4000,
    "MaxHistoryMessages": 100
  }
}
```

## Health Checks

- **Liveness:** `http://localhost:5000/health/live`
- **Readiness:** `http://localhost:5000/health/ready` (checks Redis connectivity, reuses the singleton `IConnectionMultiplexer`)

## Observability

OpenTelemetry is configured with ASP.NET Core, runtime, and gRPC-client instrumentation. The application exposes the following custom metrics from the `Chat.Server` meter:

| Metric | Type | Attributes | Description |
|---|---|---|---|
| `chat.connections.active` | UpDownCounter | — | Currently connected users |
| `chat.joins.total` | Counter | `outcome`: `success` / `rejected` | Join attempts |
| `chat.messages.sent` | Counter | `type`: `user` / `system` | Messages broadcast |
| `chat.messages.bytes` | Histogram | `type`: `user` / `system` | Broadcast payload sizes (bytes) |
| `chat.group.broadcast.duration` | Histogram | `type`: `user` / `system` | Redis group broadcast latency (ms) |
| `chat.notification.errors` | Counter | — | `ServerNotificationService` loop failures |

The console exporter is enabled by default. Swap it for OTLP or Prometheus in the `.WithMetrics()` / `.WithTracing()` builders in `Program.cs`.

## Project Structure

```
Chat/
├── src/
│   ├── Chat.Contracts/          # Shared interfaces and models
│   │   ├── IChatHub.cs         # Hub interface
│   │   ├── IChatHubReceiver.cs # Client receiver interface
│   │   ├── MessageData.cs      # Message model
│   │   └── Constants.cs        # Shared constants
│   ├── Chat.Server/            # Server application
│   │   ├── Hubs/
│   │   │   └── ChatHub.cs      # Hub implementation
│   │   ├── Services/
│   │   │   ├── ServerNotificationService.cs  # Periodic notifications
│   │   │   └── ChatHistoryService.cs         # Redis-backed message history
│   │   ├── Configuration/
│   │   │   └── ChatOptions.cs  # App configuration
│   │   ├── NoOpReceiver.cs     # Dummy receiver for server-side hub client
│   │   ├── Program.cs          # App entry point
│   │   ├── appsettings.json
│   │   └── Dockerfile
│   └── Chat.Client/            # Console client
│       ├── ChatClient.cs       # Connection lifecycle and auto-reconnect
│       ├── ConsoleUI.cs        # Thread-safe coloured console output
│       ├── ChatReceiver.cs     # Message receiver implementation
│       ├── Configuration/
│       │   └── ClientOptions.cs  # Client configuration
│       ├── Program.cs
│       └── Dockerfile
├── docs/                       # Documentation
│   ├── next-steps.md          # Roadmap and code review findings
│   └── plans/                 # Implementation plans
├── compose.yaml               # Docker Compose orchestration
└── Chat.slnx                  # Solution file
```

## How Multi-Server Works

MagicOnion 7.x uses the **Multicaster** library for Redis-based group distribution:

```csharp
// Program.cs - Enable Redis backplane
builder.Services.AddMagicOnion()
    .UseRedisGroup(
        config => config.ConnectionString = redisConnectionString,
        registerAsDefault: true);  // Critical: must be true for cross-server
```

When `registerAsDefault: true`:
1. All `group.All.OnReceiveMessage()` calls are published to Redis
2. All server instances subscribe to the same Redis channel
3. Each server broadcasts to its local clients

## Testing Multi-Instance Setup

1. Start with Docker Compose: `docker compose up --build`
2. Connect client to instance 1: `docker compose run --rm chat.client` (enter `http://chat.server:8080`)
3. Connect another client to instance 2: `docker compose run --rm chat.client` (enter `http://chat.server.2:8080`)
4. Send messages from either client - both receive messages via Redis backplane

## Production Considerations

✅ **Implemented:**
- Structured logging with Serilog
- Health checks (liveness and readiness, Redis reuses singleton connection)
- Message serialization with MessagePack
- Multi-instance support via MagicOnion Redis backplane
- Docker health checks
- OpenTelemetry metrics and tracing with console exporter (application metrics, ASP.NET Core, runtime, gRPC client)
- NGINX rate limiting (100 req/s per IP, burst 50)
- Kestrel connection limits (1 000 concurrent connections, 100 HTTP/2 streams per connection)
- Graceful shutdown in `ServerNotificationService` with reconnection loop
- `DateTimeOffset` timestamps for round-trip-safe MessagePack serialization
- Redis-backed chat history (`LPUSH`+`LTRIM`, configurable cap, replayed on connect)
- Server-side message-length validation
- Client auto-reconnect with configurable retry (`MaxReconnectAttempts`)

See [docs/next-steps.md](docs/next-steps.md) for the full roadmap.

🔜 **Consider for production:**
- Authentication & authorization
- Durable message persistence (database — Redis history is ephemeral if Redis data is lost)
- User presence tracking
- TLS/SSL certificates
- Swap console exporter for OTLP or Prometheus

## License

MIT
