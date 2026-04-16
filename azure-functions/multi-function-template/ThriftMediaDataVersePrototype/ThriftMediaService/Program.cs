using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ThriftMediaService.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Register Dataverse connection service
builder.Services.AddSingleton<IDataverseConnectionService, DataverseConnectionService>();

// Register services
builder.Services.AddScoped<IStoreService, StoreService>();
builder.Services.AddScoped<IMediaService, MediaService>();

builder.Build().Run();
