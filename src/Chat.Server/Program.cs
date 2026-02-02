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
