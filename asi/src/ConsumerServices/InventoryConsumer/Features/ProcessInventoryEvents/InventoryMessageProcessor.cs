using System.Diagnostics;
using System.Text.Json;
using Azure;
using Azure.Messaging.ServiceBus;
using InventoryConsumer.Shared.Configuration;
using FluentValidation;
using Microsoft.Extensions.Options;
using Shared.Events;

namespace InventoryConsumer.Features.ProcessInventoryEvents;

/// <summary>
/// Background service that processes inventory change events from Service Bus.
/// Runs continuously, polling the Service Bus subscription.
/// </summary>
public class InventoryMessageProcessor : BackgroundService
{
    private readonly ILogger<InventoryMessageProcessor> _logger;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ServiceBusOptions _serviceBusOptions;
    private ServiceBusProcessor? _processor;

    private static readonly ActivitySource ActivitySource = new("InventoryConsumer");

    public InventoryMessageProcessor(
        ILogger<InventoryMessageProcessor> logger,
        ServiceBusClient serviceBusClient,
        IServiceProvider serviceProvider,
        IOptions<ServiceBusOptions> serviceBusOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceBusClient = serviceBusClient ?? throw new ArgumentNullException(nameof(serviceBusClient));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _serviceBusOptions = serviceBusOptions?.Value ?? throw new ArgumentNullException(nameof(serviceBusOptions));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var topicName = _serviceBusOptions.TopicName;
        var subscriptionName = _serviceBusOptions.SubscriptionName;

        var processorOptions = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 5, // Process up to 5 messages concurrently
            AutoCompleteMessages = false, // Manual message completion for full control
            MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5),
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            PrefetchCount = 10, // Prefetch for better throughput
            SubQueue = SubQueue.None
        };

        _processor = _serviceBusClient.CreateProcessor(topicName, subscriptionName, processorOptions);

        // Attach event handlers
        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        // Start processing
        await _processor.StartProcessingAsync(stoppingToken);

        _logger.LogInformation(
            "Inventory message processor started: Topic={Topic}, Subscription={Subscription}",
            topicName,
            subscriptionName);

        // Keep the service running
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // Expected when cancellation is requested
            _logger.LogInformation("Inventory message processor is stopping...");
        }
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        using var activity = ActivitySource.StartActivity("ProcessInventoryMessage", ActivityKind.Consumer);

        var messageId = args.Message.MessageId;
        var correlationId = args.Message.CorrelationId;
        var sessionId = args.Message.SessionId;

        activity?.SetTag("messaging.message_id", messageId);
        activity?.SetTag("messaging.correlation_id", correlationId);
        activity?.SetTag("messaging.session_id", sessionId);

        try
        {
            // Create a scope for scoped services (repository, transformer, validator)
            using var scope = _serviceProvider.CreateScope();

            var repository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
            var transformer = scope.ServiceProvider.GetRequiredService<IEventTransformer>();
            var validator = scope.ServiceProvider.GetRequiredService<IValidator<InventoryChangedEvent>>();

            // Deserialize the message body
            var messageBody = args.Message.Body.ToString();
            var @event = JsonSerializer.Deserialize<InventoryChangedEvent>(messageBody);

            if (@event == null)
            {
                _logger.LogError("Failed to deserialize message body: MessageId={MessageId}", messageId);
                await args.DeadLetterMessageAsync(
                    args.Message,
                    deadLetterReason: "DeserializationFailed",
                    deadLetterErrorDescription: "Message body could not be deserialized to InventoryChangedEvent");
                return;
            }

            _logger.LogInformation(
                "Processing inventory event: EventId={EventId}, InventoryItemId={InventoryItemId}, ProductId={ProductId}, ChangeType={ChangeType}, MessageId={MessageId}",
                @event.EventId,
                @event.InventoryItemId,
                @event.ProductId,
                @event.ChangeType,
                messageId);

            activity?.SetTag("event.event_id", @event.EventId);
            activity?.SetTag("event.inventory_item_id", @event.InventoryItemId);
            activity?.SetTag("event.product_id", @event.ProductId);
            activity?.SetTag("event.change_type", @event.ChangeType);

            // Validate the event
            var validationResult = await validator.ValidateAsync(@event, args.CancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning(
                    "Inventory event validation failed: EventId={EventId}, Errors={Errors}",
                    @event.EventId,
                    errors);

                await args.DeadLetterMessageAsync(
                    args.Message,
                    deadLetterReason: "ValidationFailed",
                    deadLetterErrorDescription: errors);
                return;
            }

            // Transform to document
            var document = transformer.TransformToDocument(@event);

            // Upsert to Table Storage
            await repository.UpsertInventoryAsync(document, args.CancellationToken);

            // Complete the message
            await args.CompleteMessageAsync(args.Message);

            _logger.LogInformation(
                "Successfully processed inventory event: EventId={EventId}, InventoryItemId={InventoryItemId}, MessageId={MessageId}",
                @event.EventId,
                @event.InventoryItemId,
                messageId);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing inventory message: MessageId={MessageId}, CorrelationId={CorrelationId}",
                messageId,
                correlationId);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Don't dead-letter on transient errors - let it retry
            // The message will be automatically retried based on MaxDeliveryCount
            throw;
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Service Bus processor error: Source={ErrorSource}, FullyQualifiedNamespace={Namespace}, EntityPath={EntityPath}",
            args.ErrorSource,
            args.FullyQualifiedNamespace,
            args.EntityPath);

        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stopping inventory message processor...");

        if (_processor != null)
        {
            await _processor.StopProcessingAsync(stoppingToken);
            await _processor.DisposeAsync();
        }

        await base.StopAsync(stoppingToken);
        _logger.LogInformation("Inventory message processor stopped");
    }
}
