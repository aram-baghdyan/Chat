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
│   │   │   └── ServerNotificationService.cs  # Periodic notifications
│   │   ├── Configuration/
│   │   │   └── ChatOptions.cs  # App configuration
│   │   ├── NoOpReceiver.cs     # Dummy receiver for server-side hub client
│   │   ├── Program.cs          # App entry point
│   │   ├── appsettings.json
│   │   └── Dockerfile
│   └── Chat.Client/            # Console client
│       ├── Program.cs
│       ├── ChatReceiver.cs     # Message receiver implementation
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
- Health checks (liveness and readiness)
- Message serialization with MessagePack
- Multi-instance support via MagicOnion Redis backplane
- Docker health checks

⚠️ **Pending Code Review Fixes:**
- Resource disposal in `ServerNotificationService`
- Reconnection logic for background services
- Fire-and-forget hub methods (`void` returns)
- Message ID for idempotency

See [docs/next-steps.md](docs/next-steps.md) for detailed findings.

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
