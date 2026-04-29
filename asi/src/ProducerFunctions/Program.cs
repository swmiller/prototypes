using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ProducerFunctions.Shared.Configuration;

var builder = FunctionsApplication.CreateBuilder(args);

// Add secrets support for development (local.settings.json for non-sensitive, user-secrets for sensitive)
builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: true);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Register configuration options with validation.
builder.Services
    .AddOptions<ServiceBusOptions>()
    .Bind(builder.Configuration.GetSection(ServiceBusOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<ProducerOptions>()
    .Bind(builder.Configuration.GetSection(ProducerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Register Service Bus client and publisher as singletons for connection reuse.
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<ServiceBusOptions>>().Value;
    if (!string.IsNullOrEmpty(options.ConnectionString))
    {
        // Use connection string for local development
        return new Azure.Messaging.ServiceBus.ServiceBusClient(options.ConnectionString);
    }
    else
    {
        // Use Managed Identity in production (no connection string)
        return new Azure.Messaging.ServiceBus.ServiceBusClient(options.FullyQualifiedNamespace, new Azure.Identity.DefaultAzureCredential());
    }

    throw new InvalidOperationException(
            "ServiceBus configuration invalid. Provide either ConnectionString (local) or Namespace (production)");
});

builder.Build().Run();
