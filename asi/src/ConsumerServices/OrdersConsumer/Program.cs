using Azure.Identity;
using Azure.Messaging.ServiceBus;
using OrdersConsumer.Features.ProcessOrderEvents;
using OrdersConsumer.Shared.Configuration;
using FluentValidation;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shared.Events;

var builder = WebApplication.CreateBuilder(args);

// ========== Configuration ==========

// Add user secrets support (reads from dotnet user-secrets)
builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: true);

// Register and validate options
builder.Services
    .AddOptions<ServiceBusOptions>()
    .Bind(builder.Configuration.GetSection(ServiceBusOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<TableStorageOptions>()
    .Bind(builder.Configuration.GetSection(TableStorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ========== Azure Service Bus Client ==========

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ServiceBusOptions>>().Value;

    if (!string.IsNullOrEmpty(options.ConnectionString))
    {
        // Local development: use connection string from user secrets
        return new ServiceBusClient(options.ConnectionString);
    }
    else
    {
        // Production: use Managed Identity
        return new ServiceBusClient(
            options.FullyQualifiedNamespace,
            new DefaultAzureCredential());
    }
});

// ========== Application Services ==========

// Register repository, transformer, and validator
builder.Services.AddSingleton<IOrderRepository, OrderTableRepository>();
builder.Services.AddSingleton<IEventTransformer, OrderEventTransformer>();
builder.Services.AddValidatorsFromAssemblyContaining<OrderEventValidator>();

// Register the background message processor
builder.Services.AddHostedService<OrderMessageProcessor>();

// ========== Health Checks ==========

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddCheck<TableStorageHealthCheck>("table-storage", tags: new[] { "ready" });

// Note: Service Bus health check requires the subscription to exist
// We'll add it after creating the subscription in Azure

// ========== OpenTelemetry ==========

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("OrdersConsumer"); // Our custom activity source
    });

// ========== Problem Details ==========

builder.Services.AddProblemDetails();

// ========== Build Application ==========

var app = builder.Build();

// ========== Middleware Pipeline ==========

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

// ========== Health Check Endpoints ==========

// Liveness probe - always returns healthy if the process is running
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

// Readiness probe - checks if dependencies are available
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Detailed health check endpoint (all checks)
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });
        await context.Response.WriteAsync(result);
    }
});

// ========== Admin/Monitoring Endpoints ==========

// Root endpoint
app.MapGet("/", () => new
{
    service = "OrdersConsumer",
    version = "1.0.0",
    status = "running",
    endpoints = new
    {
        health_live = "/health/live",
        health_ready = "/health/ready",
        health_detailed = "/health",
        stats = "/admin/stats"
    }
});

// Admin endpoint for processing stats
app.MapGet("/admin/stats", async (IOrderRepository repository) =>
{
    var stats = await repository.GetProcessingStatsAsync();
    return Results.Ok(new
    {
        totalOrders = stats.TotalOrders,
        lastChecked = stats.LastChecked,
        timestamp = DateTimeOffset.UtcNow
    });
});

// Graceful shutdown endpoint (for testing)
app.MapPost("/admin/shutdown", (IHostApplicationLifetime lifetime) =>
{
    lifetime.StopApplication();
    return Results.Accepted();
});

// ========== Start Application ==========

app.Logger.LogInformation("OrdersConsumer is starting...");
app.Logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
app.Logger.LogInformation("Configuration loaded from: appsettings.json, user-secrets");

app.Run();

// Make Program accessible for testing
public partial class Program { }
