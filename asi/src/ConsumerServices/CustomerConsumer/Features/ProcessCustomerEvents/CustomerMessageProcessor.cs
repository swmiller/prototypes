using System.Diagnostics;
using System.Text.Json;
using Azure;
using Azure.Messaging.ServiceBus;
using CustomerConsumer.Shared.Configuration;
using FluentValidation;
using Microsoft.Extensions.Options;
using Shared.Events;

namespace CustomerConsumer.Features.ProcessCustomerEvents;

/// <summary>
/// Background service that processes customer change events from Service Bus.
/// Runs continuously, polling the Service Bus subscription.
/// </summary>
public class CustomerMessageProcessor : BackgroundService
{
    private readonly ILogger<CustomerMessageProcessor> _logger;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ServiceBusOptions _serviceBusOptions;
    private ServiceBusProcessor? _processor;

    private static readonly ActivitySource ActivitySource = new("CustomerConsumer");

    public CustomerMessageProcessor(
        ILogger<CustomerMessageProcessor> logger,
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
            "Customer message processor started: Topic={Topic}, Subscription={Subscription}",
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
            _logger.LogInformation("Customer message processor is stopping...");
        }
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        using var activity = ActivitySource.StartActivity("ProcessCustomerMessage", ActivityKind.Consumer);

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

            var repository = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
            var transformer = scope.ServiceProvider.GetRequiredService<IEventTransformer>();
            var validator = scope.ServiceProvider.GetRequiredService<IValidator<CustomerChangedEvent>>();

            // Deserialize the message body
            var customerEvent = JsonSerializer.Deserialize<CustomerChangedEvent>(
                args.Message.Body.ToString(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (customerEvent == null)
            {
                _logger.LogWarning(
                    "Received null customer event, dead-lettering message {MessageId}",
                    messageId);
                await args.DeadLetterMessageAsync(
                    args.Message,
                    "InvalidPayload",
                    "Event deserialized to null",
                    args.CancellationToken);
                return;
            }

            _logger.LogInformation(
                "Processing customer event: CustomerId={CustomerId}, ChangeType={ChangeType}, MessageId={MessageId}",
                customerEvent.CustomerId,
                customerEvent.ChangeType,
                messageId);

            // Validate the event using FluentValidation
            var validationResult = await validator.ValidateAsync(customerEvent, args.CancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning(
                    "Customer event validation failed for {MessageId}: {Errors}",
                    messageId,
                    errors);

                await args.DeadLetterMessageAsync(
                    args.Message,
                    "ValidationFailure",
                    errors,
                    args.CancellationToken);
                return;
            }

            // Transform event to document
            var customerDocument = transformer.TransformToDocument(customerEvent);

            // Upsert to Table Storage (idempotent operation)
            await repository.UpsertCustomerAsync(customerDocument, args.CancellationToken);

            // Complete the message (remove from queue)
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);

            _logger.LogInformation(
                "Successfully processed customer event: CustomerId={CustomerId}, ChangeType={ChangeType}, MessageId={MessageId}",
                customerEvent.CustomerId,
                customerEvent.ChangeType,
                messageId);

            activity?.SetTag("processing.status", "success");
            activity?.SetTag("customer.id", customerEvent.CustomerId);
            activity?.SetTag("customer.change_type", customerEvent.ChangeType);
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Failed to deserialize customer event, dead-lettering message {MessageId}",
                messageId);

            await args.DeadLetterMessageAsync(
                args.Message,
                "DeserializationFailure",
                ex.Message,
                args.CancellationToken);

            activity?.SetTag("processing.status", "dead-lettered");
            activity?.SetTag("error.type", "deserialization");
        }
        catch (RequestFailedException ex) when (ex.Status == 429)
        {
            // Azure Table Storage throttling (429 Too Many Requests)
            _logger.LogWarning(
                ex,
                "Table Storage throttled (429), abandoning message {MessageId} for retry",
                messageId);

            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);

            activity?.SetTag("processing.status", "throttled");
            activity?.SetTag("error.type", "throttling");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process customer event for MessageId={MessageId}",
                messageId);

            // Dead-letter after max retries (check delivery count)
            if (args.Message.DeliveryCount >= 3)
            {
                _logger.LogError(
                    "Message {MessageId} has been delivered {DeliveryCount} times, moving to dead-letter queue",
                    messageId,
                    args.Message.DeliveryCount);

                await args.DeadLetterMessageAsync(
                    args.Message,
                    "ProcessingFailure",
                    $"Failed after {args.Message.DeliveryCount} attempts: {ex.Message}",
                    args.CancellationToken);

                activity?.SetTag("processing.status", "dead-lettered");
            }
            else
            {
                // Abandon for retry
                _logger.LogWarning(
                    "Abandoning message {MessageId} for retry (Attempt {DeliveryCount})",
                    messageId,
                    args.Message.DeliveryCount);

                await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);

                activity?.SetTag("processing.status", "abandoned");
            }

            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Service Bus processor error: Source={ErrorSource}, Entity={EntityPath}, Message={ErrorMessage}",
            args.ErrorSource,
            args.EntityPath,
            args.Exception.Message);

        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stopping customer message processor...");

        if (_processor != null)
        {
            await _processor.StopProcessingAsync(stoppingToken);
            await _processor.DisposeAsync();
        }

        await base.StopAsync(stoppingToken);

        _logger.LogInformation("Customer message processor stopped");
    }
}