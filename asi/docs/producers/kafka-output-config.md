# Kafka Output Configuration

## Overview

Azure Functions producers use Kafka output bindings to publish events to Kafka topics. This document defines the configuration and best practices for Kafka output bindings.

## Kafka Output Binding Configuration

### Basic Configuration

```csharp
[Function("inventory-item-http")]
public async Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
    [KafkaOutput(
        topic: "inventory.updated",
        BrokerList = "%KafkaBootstrapServers%",
        ConnectionStringSetting = "KafkaConnectionString",
        Protocol = BrokerProtocol.SaslSsl,
        AuthenticationMode = BrokerAuthenticationMode.Plain
    )]
    IAsyncCollector<KafkaEventData<string>> events)
{
    // Function implementation
}
```

### Configuration Options

| Setting                   | Description                            | Example                          |
| ------------------------- | -------------------------------------- | -------------------------------- |
| `topic`                   | Kafka topic name                       | `"inventory.updated"`            |
| `BrokerList`              | Kafka broker addresses                 | `"%KafkaBootstrapServers%"`      |
| `ConnectionStringSetting` | App setting name for connection string | `"KafkaConnectionString"`        |
| `Protocol`                | Communication protocol                 | `BrokerProtocol.SaslSsl`         |
| `AuthenticationMode`      | Auth mechanism                         | `BrokerAuthenticationMode.Plain` |
| `MaxMessageBytes`         | Max message size                       | `1000000` (1MB)                  |
| `BatchSize`               | Batch size for publishing              | `100`                            |

## Connection String Formats

### Azure Event Hubs for Kafka

```
Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=your-key;EntityPath=your-topic
```

Store in Key Vault and reference in Function App settings:

```json
{
  "KafkaConnectionString": "@Microsoft.KeyVault(SecretUri=https://your-vault.vault.azure.net/secrets/kafka-connection/version)"
}
```

### Confluent Cloud

```
bootstrap.servers=your-cluster.confluent.cloud:9092
sasl.mechanisms=PLAIN
sasl.username=your-api-key
sasl.password=your-api-secret
security.protocol=SASL_SSL
```

## Event Data Structure

### Simple String Events

```csharp
var kafkaEvent = new KafkaEventData<string>
{
    Key = entityId,  // Partition key
    Value = JsonSerializer.Serialize(eventData)
};

await events.AddAsync(kafkaEvent);
```

### Events with Headers

```csharp
var kafkaEvent = new KafkaEventData<string>
{
    Key = customerId,
    Value = JsonSerializer.Serialize(customerEvent),
    Headers = new List<KafkaHeader>
    {
        new KafkaHeader { Key = "event-type", Value = Encoding.UTF8.GetBytes("customer.created") },
        new KafkaHeader { Key = "correlation-id", Value = Encoding.UTF8.GetBytes(correlationId) },
        new KafkaHeader { Key = "source-system", Value = Encoding.UTF8.GetBytes("crm-api") },
        new KafkaHeader { Key = "schema-version", Value = Encoding.UTF8.GetBytes("1.0") }
    }
};

await events.AddAsync(kafkaEvent);
```

## Partitioning Strategy

### Partition by Entity ID (Recommended)

```csharp
// Ensures all events for the same entity go to the same partition
// Maintains ordering for a single entity
var kafkaEvent = new KafkaEventData<string>
{
    Key = inventoryItemId,  // Kafka uses hash(key) % partition_count
    Value = eventJson
};
```

### Partition by Domain Aggregate

```csharp
// For orders, use orderId
var kafkaEvent = new KafkaEventData<string>
{
    Key = orderId,
    Value = orderEventJson
};

// For customers, use customerId
var kafkaEvent = new KafkaEventData<string>
{
    Key = customerId,
    Value = customerEventJson
};
```

### No Key (Round-Robin)

```csharp
// Events distributed evenly across partitions
// No ordering guarantees
var kafkaEvent = new KafkaEventData<string>
{
    Key = null,
    Value = eventJson
};
```

## Batching Strategy

### Batch Publishing Pattern

```csharp
[Function("inventory-batch-timer")]
public async Task Run(
    [TimerTrigger("0 */5 * * * *")] TimerInfo timer,
    [KafkaOutput("inventory.updated",
        BrokerList = "%KafkaBootstrapServers%",
        ConnectionStringSetting = "KafkaConnectionString",
        BatchSize = 100,
        LingerMs = 10
    )]
    IAsyncCollector<KafkaEventData<string>> events)
{
    var items = await _inventoryService.GetRecentChangesAsync();
    var batch = new List<KafkaEventData<string>>();

    foreach (var item in items)
    {
        batch.Add(CreateEvent(item));

        // Flush batch when it reaches optimal size
        if (batch.Count >= 100)
        {
            await events.FlushAsync();
            batch.Clear();
        }
    }

    // Flush remaining
    if (batch.Any())
    {
        await events.FlushAsync();
    }
}
```

### Batch Configuration

| Parameter         | Description                   | Recommended Value  |
| ----------------- | ----------------------------- | ------------------ |
| `BatchSize`       | Max records per batch         | 100-1000           |
| `LingerMs`        | Wait time to accumulate batch | 10-100ms           |
| `MaxInFlight`     | Max unacknowledged requests   | 5                  |
| `CompressionType` | Compression algorithm         | `Gzip` or `Snappy` |

## Error Handling

### Retry Configuration

```csharp
public class KafkaOutputConfig
{
    public const int MaxRetries = 3;
    public const int RetryBackoffMs = 100;
    public const int RequestTimeoutMs = 30000;
}

[KafkaOutput("inventory.updated",
    BrokerList = "%KafkaBootstrapServers%",
    ConnectionStringSetting = "KafkaConnectionString",
    MaxRetry = KafkaOutputConfig.MaxRetries,
    RetryBackoffMs = KafkaOutputConfig.RetryBackoffMs,
    RequestTimeoutMs = KafkaOutputConfig.RequestTimeoutMs
)]
```

### Handling Publish Failures

```csharp
try
{
    await events.AddAsync(kafkaEvent);
    _logger.LogInformation("Published event {EventType} with key {Key}",
        eventType, kafkaEvent.Key);
}
catch (KafkaException ex) when (ex.Error.Code == ErrorCode.MsgSizeTooLarge)
{
    _logger.LogError(ex, "Event too large: {Size} bytes", eventJson.Length);
    // Split or compress the event
    throw;
}
catch (KafkaException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
{
    _logger.LogError(ex, "Topic does not exist: {Topic}", topic);
    // Alert operations - configuration issue
    throw;
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to publish event");
    // Write to dead-letter queue
    await _deadLetterQueue.SendAsync(kafkaEvent);
    throw;
}
```

## Monitoring and Metrics

### Custom Metrics

```csharp
public class KafkaPublishMetrics
{
    private readonly ILogger _logger;
    private readonly TelemetryClient _telemetry;

    public async Task<bool> PublishWithMetricsAsync(
        IAsyncCollector<KafkaEventData<string>> events,
        KafkaEventData<string> kafkaEvent,
        string eventType)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await events.AddAsync(kafkaEvent);
            stopwatch.Stop();

            _telemetry.TrackMetric("kafka.publish.duration", stopwatch.ElapsedMilliseconds);
            _telemetry.TrackMetric("kafka.publish.success", 1);
            _telemetry.TrackEvent("KafkaEventPublished", new Dictionary<string, string>
            {
                ["EventType"] = eventType,
                ["Topic"] = kafkaEvent.Topic,
                ["PartitionKey"] = kafkaEvent.Key
            });

            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _telemetry.TrackMetric("kafka.publish.failure", 1);
            _telemetry.TrackException(ex);
            return false;
        }
    }
}
```

### Application Insights Queries

```kusto
// Track publish success rate
customMetrics
| where name == "kafka.publish.success" or name == "kafka.publish.failure"
| summarize SuccessCount = sumif(value, name == "kafka.publish.success"),
            FailureCount = sumif(value, name == "kafka.publish.failure")
| extend SuccessRate = SuccessCount * 100.0 / (SuccessCount + FailureCount)

// Track publish latency
customMetrics
| where name == "kafka.publish.duration"
| summarize avg(value), percentile(value, 95), percentile(value, 99) by bin(timestamp, 5m)
```

## Best Practices

### 1. Use Correlation IDs

```csharp
var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();

var kafkaEvent = new KafkaEventData<string>
{
    Key = entityId,
    Value = eventJson,
    Headers = new List<KafkaHeader>
    {
        new KafkaHeader {
            Key = "correlation-id",
            Value = Encoding.UTF8.GetBytes(correlationId)
        }
    }
};
```

### 2. Schema Versioning

```csharp
var envelope = new EventEnvelope
{
    EventType = "inventory.updated",
    SchemaVersion = "1.0",
    CorrelationId = correlationId,
    Timestamp = DateTimeOffset.UtcNow,
    SourceSystem = "inventory-api",
    Payload = inventoryUpdateData
};

var kafkaEvent = new KafkaEventData<string>
{
    Key = inventoryItemId,
    Value = JsonSerializer.Serialize(envelope)
};
```

### 3. Event Size Management

```csharp
const int MaxEventSize = 900_000; // 900KB (leave buffer below 1MB limit)

var eventJson = JsonSerializer.Serialize(eventData);

if (Encoding.UTF8.GetByteCount(eventJson) > MaxEventSize)
{
    // Option 1: Compress
    var compressed = Compress(eventJson);

    // Option 2: Split into multiple events
    var chunks = SplitEvent(eventData);
    foreach (var chunk in chunks)
    {
        await events.AddAsync(CreateEvent(chunk));
    }

    // Option 3: Store large payload in Blob, publish reference
    var blobUri = await _blobStorage.UploadAsync(eventData);
    await events.AddAsync(CreateReferenceEvent(blobUri));
}
else
{
    await events.AddAsync(new KafkaEventData<string>
    {
        Key = entityId,
        Value = eventJson
    });
}
```

### 4. Topic Naming Convention

Use consistent topic naming across all producers:

```
{domain}.{entity}.{action}
```

Examples:

- `inventory.item.updated`
- `inventory.transfer.created`
- `orders.order.created`
- `orders.payment.captured`
- `customers.customer.updated`
- `products.product.created`
- `pricing.price.changed`

### 5. Environment-Specific Configuration

```json
{
  "Development": {
    "KafkaBootstrapServers": "localhost:9092",
    "KafkaConnectionString": ""
  },
  "Staging": {
    "KafkaBootstrapServers": "kafka-staging.contoso.com:9093",
    "KafkaConnectionString": "@Microsoft.KeyVault(...)"
  },
  "Production": {
    "KafkaBootstrapServers": "kafka-prod.contoso.com:9093",
    "KafkaConnectionString": "@Microsoft.KeyVault(...)"
  }
}
```

## Sample Complete Function

```csharp
public class InventoryProducerFunction
{
    private readonly ILogger<InventoryProducerFunction> _logger;
    private readonly KafkaPublishMetrics _metrics;

    public InventoryProducerFunction(
        ILogger<InventoryProducerFunction> logger,
        KafkaPublishMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    [Function("inventory-item-http")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [KafkaOutput("inventory.updated",
            BrokerList = "%KafkaBootstrapServers%",
            ConnectionStringSetting = "KafkaConnectionString",
            Protocol = BrokerProtocol.SaslSsl,
            AuthenticationMode = BrokerAuthenticationMode.Plain,
            BatchSize = 100,
            MaxRetry = 3
        )]
        IAsyncCollector<KafkaEventData<string>> events)
    {
        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();

        try
        {
            // 1. Extract and validate
            var payload = await req.ReadFromJsonAsync<InventoryUpdateRequest>();
            if (payload == null || string.IsNullOrEmpty(payload.InventoryItemId))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            // 2. Create event envelope
            var envelope = new EventEnvelope
            {
                EventType = "inventory.updated",
                SchemaVersion = "1.0",
                CorrelationId = correlationId,
                Timestamp = DateTimeOffset.UtcNow,
                SourceSystem = "inventory-api",
                Payload = new InventoryUpdatedEvent
                {
                    InventoryItemId = payload.InventoryItemId,
                    Sku = payload.Sku,
                    QuantityOnHand = payload.QuantityOnHand,
                    LocationId = payload.LocationId
                }
            };

            // 3. Publish to Kafka
            var kafkaEvent = new KafkaEventData<string>
            {
                Key = payload.InventoryItemId,
                Value = JsonSerializer.Serialize(envelope),
                Headers = new List<KafkaHeader>
                {
                    new KafkaHeader { Key = "event-type", Value = Encoding.UTF8.GetBytes("inventory.updated") },
                    new KafkaHeader { Key = "correlation-id", Value = Encoding.UTF8.GetBytes(correlationId) },
                    new KafkaHeader { Key = "schema-version", Value = Encoding.UTF8.GetBytes("1.0") }
                }
            };

            var published = await _metrics.PublishWithMetricsAsync(
                events,
                kafkaEvent,
                "inventory.updated");

            if (published)
            {
                _logger.LogInformation(
                    "Published inventory.updated event for {InventoryItemId} with correlation {CorrelationId}",
                    payload.InventoryItemId,
                    correlationId);

                var response = req.CreateResponse(HttpStatusCode.Accepted);
                await response.WriteAsJsonAsync(new { correlationId });
                return response;
            }
            else
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process inventory update with correlation {CorrelationId}",
                correlationId);
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }
}
```
