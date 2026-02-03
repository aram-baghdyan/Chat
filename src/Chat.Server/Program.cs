using Chat.Server.Configuration;
using Chat.Server.Services;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
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

    // MagicOnion gRPC services
    builder.Services.AddGrpc(options =>
    {
        options.MaxReceiveMessageSize = 4 * 1024 * 1024; // 4 MB
        options.MaxSendMessageSize = 4 * 1024 * 1024;
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    });

    builder.Services.AddMagicOnion()
        .UseRedisGroup(config => config.ConnectionString = redisConnectionString, true);

    // gRPC reflection for development
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddGrpcReflection();
    }

    // OpenTelemetry metrics and tracing
    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter("Chat.Server"))
        .WithTracing(tracing => tracing
            .AddGrpcClientInstrumentation()
            .AddAspNetCoreInstrumentation()
            .AddSource("Chat.Server"));

    // Background services
    builder.Services.AddHostedService<ServerNotificationService>();

    // Health checks — reuse the singleton IConnectionMultiplexer
    builder.Services
        .AddHealthChecks()
        .AddRedis(
            sp => sp.GetRequiredService<IConnectionMultiplexer>(),
            name: "redis",
            tags: ["ready"]);

    // Kestrel connection limits to prevent resource exhaustion
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxConcurrentConnections = 1000;
        options.Limits.MaxConcurrentUpgradedConnections = 1000;
        options.Limits.Http2.MaxStreamsPerConnection = 100;
    });

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
