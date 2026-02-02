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
