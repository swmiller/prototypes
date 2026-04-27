# Azure Functions Project Guidelines

## Framework Requirements

- **Target Framework**: .NET 10
- All `dotnet` commands should target .NET 10 (e.g., `dotnet new func --framework net10.0`)
- Use .NET 10 SDK for builds, tests, and deployments
- Specify `<TargetFramework>net10.0</TargetFramework>` in `.csproj` files

## Architecture Patterns

### Producers vs Consumers

- **Producers**: Use Azure Functions (isolated worker model) for event generation
  - Timer-triggered, HTTP-triggered, or event-triggered functions
  - Lightweight, serverless, and auto-scaling
  - Publish events to Service Bus Topics

- **Consumers**: Use ASP.NET 10 Minimal APIs (not Azure Functions) for Service Bus message processing
  - Implement as long-running background services with `IHostedService`
  - Use `ServiceBusProcessor` for continuous message processing
  - Deploy to Azure Container Apps or AKS for better control and observability
  - Expose health check endpoints (`/health/live`, `/health/ready`)
  - Enable OpenTelemetry for metrics and distributed tracing
  - Refer to architecture documentation for detailed consumer implementation patterns

## Azure Functions Best Practices

**Note**: These practices apply to Azure Functions projects (producers). For Service Bus consumers, use ASP.NET 10 Minimal APIs instead (see Architecture Patterns section).

### Function Structure

- Use **isolated worker model** (.NET 10) for better performance and dependency injection support
- One function per file in domain-specific folders (e.g., `Features/Inventory/`, `Features/Orders/`)
- Keep function classes focused on orchestration, delegate business logic to services
- Use descriptive function names that reflect the trigger and domain (e.g., `InventoryTimerProducer`, `OrderTimerProducer`)

### Trigger Configuration

- **Timer triggers**: Use NCRONTAB expressions for scheduling
- **HTTP triggers**: Set appropriate authorization level (Function, Anonymous, Admin)
- **Service Bus triggers**: Configure session support, max concurrent calls, and auto-complete behavior
- Always specify explicit trigger properties rather than relying on defaults

### Configuration Management

- Use strongly-typed configuration with `IOptions<T>` pattern
- Store connection strings and secrets in Azure Key Vault, reference via `@Microsoft.KeyVault(...)`
- **Local Development**: Use `dotnet user-secrets` for sensitive data (never commit secrets to source control)
- Use `local.settings.json` for non-sensitive configuration only
- Environment-specific settings via App Settings in Azure Portal

### Secrets Management

**Local Development with dotnet user-secrets:**

```bash
# Initialize user secrets (run once per project)
dotnet user-secrets init

# Add secrets
dotnet user-secrets set "ServiceBus:ConnectionString" "Endpoint=sb://..."
dotnet user-secrets set "CosmosDb:ConnectionString" "AccountEndpoint=..."

# List all secrets
dotnet user-secrets list

# Remove a secret
dotnet user-secrets remove "ServiceBus:ConnectionString"

# Clear all secrets
dotnet user-secrets clear
```

**Best Practices:**

- Initialize secrets immediately after creating the project: `dotnet user-secrets init`
- Store ALL sensitive data in user-secrets (connection strings, API keys, passwords)
- Document required secrets in README.md without exposing values
- Use hierarchical keys matching configuration structure (e.g., `"ServiceBus:Namespace"`)
- Never commit `local.settings.json` with sensitive data (add to `.gitignore`)
- User secrets are stored in: `%APPDATA%\Microsoft\UserSecrets\<user_secrets_id>\secrets.json`
- For team onboarding, provide a secrets template file without actual values

### Authentication & Security

- Use **Managed Identity** for Azure service connections (Service Bus, Cosmos DB, Storage)
- Avoid connection strings in code; use `DefaultAzureCredential` pattern
- Never hardcode secrets, API keys, or connection strings
- Register Azure clients in DI container with managed identity
- **Local Development**: Use `dotnet user-secrets` instead of storing secrets in `local.settings.json`
- Production: Use Azure Key Vault with Managed Identity (no connection strings)

### Error Handling & Resilience

- Implement structured error handling with specific exception types
- Use dead-letter queues for poison messages
- Log errors with correlation IDs for distributed tracing
- Return appropriate HTTP status codes (400 for validation, 503 for service unavailable)
- Implement retry policies using Polly for transient failures

### Observability

- Use structured logging with `ILogger<T>` and log levels (Information, Warning, Error)
- Include correlation IDs in all log statements
- Add Application Insights telemetry for custom metrics
- Use distributed tracing with `System.Diagnostics.ActivitySource`
- Log function entry/exit with key parameters (but not sensitive data)

## Dependency Injection

### Service Registration

- Register services in `Program.cs` using the appropriate lifetime:
  - **Singleton**: Stateless services, Azure clients (ServiceBusClient, CosmosClient)
  - **Scoped**: Per-request services, EF Core contexts
  - **Transient**: Lightweight, stateful services
- Use interface-based registration for testability: `services.AddSingleton<IService, ServiceImpl>()`
- Group related registrations with extension methods (e.g., `AddServiceBusServices()`)

### Constructor Injection

- Inject dependencies via constructor, never use service locator pattern
- Keep constructor parameters focused (max 5-7 dependencies; refactor if more)
- Inject `ILogger<T>` for logging, `IConfiguration` for settings
- Store injected dependencies in readonly fields

### Configuration Binding

- Use `IOptions<T>` pattern for configuration sections
- Create strongly-typed configuration classes with data annotations for validation
- Register with: `services.Configure<MyConfig>(configuration.GetSection("MyConfig"))`
- Inject as `IOptions<MyConfig>` or `IOptionsSnapshot<MyConfig>` for reloadable config

## SOLID Principles

### Single Responsibility (SRP)

- Each function handles one trigger type and domain event
- Services have one reason to change (e.g., `IInventoryRepository`, `IEventPublisher`)
- Separate concerns: Functions orchestrate, Services execute business logic, Repositories handle data

### Open/Closed (OCP)

- Design services with extensibility through interfaces and abstract classes
- Use strategy pattern for varying behaviors (e.g., different event serialization formats)
- Extend functionality via inheritance or composition, not modification

### Liskov Substitution (LSP)

- Implementations must fulfill interface contracts without surprises
- Derived classes should not weaken preconditions or strengthen postconditions
- Use abstract base classes for shared behavior across similar functions

### Interface Segregation (ISP)

- Create focused interfaces for specific capabilities (e.g., `IEventPublisher`, `IEventValidator`)
- Avoid "fat" interfaces that force implementations to depend on unused methods
- Clients should only depend on methods they actually use

### Dependency Inversion (DIP)

- Depend on abstractions (interfaces), not concrete implementations
- High-level modules (Functions) should not depend on low-level modules (Azure SDK directly)
- Both should depend on abstractions (e.g., `IServiceBusPublisher` wraps `ServiceBusClient`)

## Clean/Vertical Slice Architecture

### Folder Structure

```
src/
├── ProducerFunctions/
│   ├── Program.cs                    # DI registration, host configuration
│   ├── host.json                     # Function runtime settings
│   ├── local.settings.json           # Local development config
│   ├── Features/                     # Vertical slices by feature
│   │   ├── Inventory/
│   │   │   ├── InventoryTimerProducer.cs
│   │   │   ├── InventoryUpdatedEvent.cs
│   │   │   ├── IInventoryService.cs
│   │   │   └── InventoryService.cs
│   │   ├── Orders/
│   │   │   ├── OrderTimerProducer.cs
│   │   │   ├── OrderCreatedEvent.cs
│   │   │   ├── IOrderService.cs
│   │   │   └── OrderService.cs
│   │   └── Customers/
│   │       ├── CustomerTimerProducer.cs
│   │       ├── CustomerChangedEvent.cs
│   │       ├── ICustomerService.cs
│   │       └── CustomerService.cs
│   ├── Shared/                       # Shared infrastructure
│   │   ├── Services/
│   │   │   ├── IServiceBusPublisher.cs
│   │   │   └── ServiceBusPublisher.cs
│   │   ├── Configuration/
│   │   │   ├── ServiceBusOptions.cs
│   │   │   └── ApplicationOptions.cs
│   │   └── Extensions/
│   │       ├── ServiceCollectionExtensions.cs
│   │       └── ServiceBusMessageExtensions.cs
│   └── ProducerFunctions.csproj
```

### Vertical Slice Guidelines

- **Feature Cohesion**: Group all related code by feature/domain (Inventory, Orders, Customers)
- **Minimize Shared Code**: Only extract to `Shared/` when used by 3+ features
- **Self-Contained Features**: Each feature folder contains its function, events, services, and interfaces
- **Independent Evolution**: Features can evolve independently without affecting others
- **Clear Boundaries**: Use namespaces to enforce boundaries (e.g., `ProducerFunctions.Features.Inventory`)

### Shared Infrastructure

- **Services**: Azure service wrappers (Service Bus, Cosmos DB, storage)
- **Configuration**: Strongly-typed options classes, validation
- **Extensions**: Reusable extension methods for DI, logging, message formatting
- **Common Models**: Only for truly shared DTOs (correlation context, error responses)

## Additional Best Practices

### Testing

- Write unit tests for business logic in service classes
- Use `ILogger<T>` abstractions to avoid Application Insights dependencies in tests
- Mock Azure SDK clients using interfaces
- Integration tests with Azurite for local storage emulation
- Use `FakeTimeProvider` for testing timer triggers

### Performance

- Reuse Azure clients (ServiceBusClient, CosmosClient) via singleton registration
- Use async/await consistently, avoid blocking calls
- Configure appropriate batch sizes for Service Bus sends
- Use `ValueTask<T>` for potentially synchronous operations
- Profile with Application Insights for cold start and execution duration

### Naming Conventions

- Functions: `{Domain}{TriggerType}{Action}` (e.g., `InventoryTimerProducer`)
- Services: `I{Domain}Service` / `{Domain}Service` (e.g., `IInventoryService`)
- Events: `{Domain}{PastTenseAction}Event` (e.g., `InventoryUpdatedEvent`)
- Configuration: `{Service}Options` (e.g., `ServiceBusOptions`)

### Code Organization

- Keep functions thin (< 50 lines), delegate to services
- Use records for immutable DTOs and events
- Apply `[Function("Name")]` attribute with descriptive names
- Group usings by: System, Microsoft, Third-party, Local
- Use file-scoped namespaces for cleaner code

### Documentation

- Add XML comments to public interfaces and complex methods
- Document non-obvious configuration settings
- Include examples in code comments for complex patterns
- Keep README.md updated with setup and deployment instructions
- **Secrets Documentation**: Create a `secrets.template.json` or document required secrets in README.md
  - List all required secret keys without exposing actual values
  - Example: "Required secrets: ServiceBus:ConnectionString, CosmosDb:ConnectionString"
  - Provide setup instructions: `dotnet user-secrets set "ServiceBus:ConnectionString" "<your-value>"`

### Service Bus Specific

- **Consumers**: Implement using ASP.NET 10 Minimal APIs with background services, not Azure Functions
- Set `MessageId` to event ID for idempotency
- Use `SessionId` for ordered processing (e.g., per product)
- Include schema version in `ApplicationProperties`
- Add correlation ID for distributed tracing
- Implement proper message completion (Complete/Abandon/DeadLetter)

### Cosmos DB Specific

- Use partition keys aligned with query patterns
- Implement upsert operations for idempotency
- Handle 429 (throttling) with retry logic
- Use async methods exclusively
- Add change feed triggers for event-driven architectures

### Local Development

- Use Azurite for Azure Storage emulation
- Use Azure Service Bus Emulator or real Service Bus namespace
- **Secrets**: Use `dotnet user-secrets` for sensitive data (connection strings, API keys)
- **Configuration**: Use `local.settings.json` for non-sensitive settings only
- Use `DefaultAzureCredential` for seamless local-to-cloud transition
- Run functions with `func start` or F5 in Visual Studio
- Ensure `local.settings.json` is in `.gitignore` (Azure Functions template does this by default)

**Example local.settings.json (no secrets):**

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ServiceBusNamespace": "asi-servicebus-dev"
  }
}
```

**Secrets stored in user-secrets instead:**

```bash
dotnet user-secrets set "ServiceBus:ConnectionString" "Endpoint=sb://asi-servicebus-dev.servicebus.windows.net/..."
```

### Deployment

- Use Bicep or Terraform for infrastructure as code
- Enable Application Insights for all function apps
- Configure managed identity and RBAC permissions
- Use deployment slots for zero-downtime deployments
- Set up CI/CD pipelines with automated testing
- **Secrets in Production**:
  - Use Azure Key Vault for all secrets (never App Settings for sensitive data)
  - Enable Managed Identity on Function App
  - Reference Key Vault secrets: `@Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/secrets/MySecret/)`
  - Grant Function App identity "Key Vault Secrets User" role
  - Transition from local user-secrets to Key Vault references in App Settings
