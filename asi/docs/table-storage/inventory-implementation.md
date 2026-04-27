# Azure Table Storage Implementation for Inventory Updates

## Overview

This document describes how to implement Azure Table Storage as the data store for the `inventory-updated` Service Bus topic, including message shredding, schema design, key strategies, and idempotency patterns.

---

## Key Constraints and Characteristics

Azure Table Storage is a **key-value NoSQL store** with these important limitations:

- **No complex queries**: Only supports equality filters on PartitionKey and RowKey (efficient) and simple property filters (less efficient)
- **No joins**: Single-table queries only
- **Entity size limit**: 1 MB per entity, 255 properties max
- **No transactions across partitions**: Batch operations limited to same PartitionKey
- **Simple data types**: String, Binary, Boolean, DateTime, Double, GUID, Int32, Int64

**Trade-off**: 10-20x cheaper than Cosmos DB, but far less querying flexibility.

---

## Schema Design for `inventory-updated` Topic

Given the `InventoryUpdatedEvent` structure:

```csharp
{
  EventId: Guid,
  ProductId: string,
  WarehouseId: string,
  QuantityOnHand: int,
  QuantityReserved: int,
  QuantityAvailable: int,
  LastUpdatedBy: string,
  Timestamp: DateTimeOffset,
  SchemaVersion: string
}
```

### Strategy 1: Product-Centric (Best for CRM queries by product)

**Table**: `InventoryItems`

| Property          | Value                                | Notes                        |
| ----------------- | ------------------------------------ | ---------------------------- |
| **PartitionKey**  | `{ProductId}`                        | e.g., "PROD-8472"            |
| **RowKey**        | `{WarehouseId}`                      | e.g., "WH-SEATTLE"           |
| QuantityOnHand    | 150                                  | int32                        |
| QuantityReserved  | 25                                   | int32                        |
| QuantityAvailable | 125                                  | int32 (calculated)           |
| LastUpdatedBy     | "warehouse-system"                   | string                       |
| Timestamp         | 2026-04-27T10:30:00Z                 | DateTime                     |
| SchemaVersion     | "v1.2"                               | string                       |
| EventId           | a3f5e9c7-4d2b-4e9f-8a1c-6b3d4e5f6a7b | GUID (for idempotency/dedup) |
| ETag              | (managed by Table Storage)           | For optimistic concurrency   |

**Query Patterns**:

- ✅ Get all inventory for a product: `PartitionKey == 'PROD-8472'` (single-partition, fast)
- ✅ Get specific product + warehouse: `PartitionKey == 'PROD-8472' AND RowKey == 'WH-SEATTLE'` (point read, fastest)
- ❌ Get all inventory for a warehouse: Cross-partition query (slow, expensive)
- ❌ Get low-stock items: Must scan all partitions, filter on QuantityAvailable (very slow)

**When to use**: CRM primarily queries by product (e.g., "show me all warehouses with PROD-8472")

---

### Strategy 2: Warehouse-Centric (Best for operational queries by warehouse)

**Table**: `InventoryItems`

| Property          | Value                                | Notes              |
| ----------------- | ------------------------------------ | ------------------ |
| **PartitionKey**  | `{WarehouseId}`                      | e.g., "WH-SEATTLE" |
| **RowKey**        | `{ProductId}`                        | e.g., "PROD-8472"  |
| QuantityOnHand    | 150                                  |                    |
| QuantityReserved  | 25                                   |                    |
| QuantityAvailable | 125                                  |                    |
| LastUpdatedBy     | "warehouse-system"                   |                    |
| Timestamp         | 2026-04-27T10:30:00Z                 |                    |
| SchemaVersion     | "v1.2"                               |                    |
| EventId           | a3f5e9c7-4d2b-4e9f-8a1c-6b3d4e5f6a7b |                    |

**Query Patterns**:

- ✅ Get all inventory in a warehouse: `PartitionKey == 'WH-SEATTLE'` (fast)
- ✅ Get specific warehouse + product: Point read (fastest)
- ❌ Get all warehouses with a product: Cross-partition query (slow)

**When to use**: Warehouse-focused operations, facility management

---

### Strategy 3: Dual-Table Pattern (Support multiple query patterns)

**Maintain two tables** with different key structures:

1. **`InventoryByProduct`**: PartitionKey = ProductId, RowKey = WarehouseId
2. **`InventoryByWarehouse`**: PartitionKey = WarehouseId, RowKey = ProductId

Consumer writes to **both tables** on each message (using batch operations when possible).

**Trade-offs**:

- ✅ Fast queries for both patterns
- ❌ 2x storage cost
- ❌ 2x write cost
- ❌ Must keep tables in sync (eventual consistency risk)

---

### Strategy 4: Composite RowKey (Time-series pattern)

For historical tracking, append timestamp to RowKey:

| Property         | Value                                  | Notes                               |
| ---------------- | -------------------------------------- | ----------------------------------- |
| **PartitionKey** | `{ProductId}_{WarehouseId}`            | e.g., "PROD-8472_WH-SEATTLE"        |
| **RowKey**       | `{Timestamp-Ticks-Inverted}_{EventId}` | e.g., "2637392640000000000_guid..." |
| QuantityOnHand   | 150                                    |                                     |
| QuantityReserved | 25                                     |                                     |

**Inverted ticks**: `long.MaxValue - DateTime.UtcNow.Ticks` (sorts newest first)

**Query Patterns**:

- ✅ Get latest inventory for product+warehouse: `PartitionKey == 'PROD-8472_WH-SEATTLE'` + take 1
- ✅ Get inventory history: Query partition, iterate by RowKey range
- ❌ Get all products in a warehouse: Cross-partition query

**When to use**: Audit trail, historical analysis, event sourcing

---

## Consumer Code Example (Shredding Message to Table Storage)

```csharp
using Azure;
using Azure.Data.Tables;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace InventoryConsumer.Services;

public class TableStorageInventoryRepository : IInventoryRepository
{
    private readonly TableClient _tableClient;
    private readonly ILogger<TableStorageInventoryRepository> _logger;

    public TableStorageInventoryRepository(
        TableServiceClient tableServiceClient,
        IConfiguration configuration,
        ILogger<TableStorageInventoryRepository> logger)
    {
        var tableName = configuration["TableStorage:InventoryTableName"] ?? "InventoryItems";
        _tableClient = tableServiceClient.GetTableClient(tableName);
        _tableClient.CreateIfNotExists();
        _logger = logger;
    }

    public async Task UpsertInventoryAsync(InventoryUpdatedEvent inventoryEvent, CancellationToken cancellationToken)
    {
        // Transform Service Bus message to Table Storage entity
        var entity = new InventoryTableEntity
        {
            PartitionKey = inventoryEvent.ProductId,  // Product-centric strategy
            RowKey = inventoryEvent.WarehouseId,
            QuantityOnHand = inventoryEvent.QuantityOnHand,
            QuantityReserved = inventoryEvent.QuantityReserved,
            QuantityAvailable = inventoryEvent.QuantityAvailable,
            LastUpdatedBy = inventoryEvent.LastUpdatedBy,
            Timestamp = inventoryEvent.Timestamp.UtcDateTime,  // ETag collision if using Timestamp property
            UpdateTimestamp = inventoryEvent.Timestamp.UtcDateTime, // Use custom property instead
            SchemaVersion = inventoryEvent.SchemaVersion,
            EventId = inventoryEvent.EventId.ToString(),
            ETag = ETag.All  // Overwrite unconditionally (last-write-wins)
        };

        try
        {
            // Upsert (idempotent) - replaces if exists, inserts if not
            await _tableClient.UpsertEntityAsync(
                entity,
                TableUpdateMode.Replace,  // Last-write-wins semantics
                cancellationToken);

            _logger.LogInformation(
                "Upserted inventory entity: ProductId={ProductId}, WarehouseId={WarehouseId}, EventId={EventId}",
                entity.PartitionKey,
                entity.RowKey,
                entity.EventId);
        }
        catch (RequestFailedException ex) when (ex.Status == 429)
        {
            // Throttling - Azure Table Storage has limits (20,000 ops/sec per table)
            _logger.LogWarning(ex, "Table Storage throttled, will retry");
            throw; // Let Service Bus processor retry with backoff
        }
    }

    public async Task<InventoryTableEntity?> GetInventoryAsync(
        string productId,
        string warehouseId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<InventoryTableEntity>(
                productId,
                warehouseId,
                cancellationToken: cancellationToken);

            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async IAsyncEnumerable<InventoryTableEntity> GetInventoryByProductAsync(
        string productId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Efficient single-partition query
        var queryResults = _tableClient.QueryAsync<InventoryTableEntity>(
            filter: $"PartitionKey eq '{productId}'",
            cancellationToken: cancellationToken);

        await foreach (var entity in queryResults.WithCancellation(cancellationToken))
        {
            yield return entity;
        }
    }

    public async IAsyncEnumerable<InventoryTableEntity> GetLowStockItemsAsync(
        int threshold,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // WARNING: Cross-partition query - expensive and slow!
        // Table Storage will scan ALL partitions
        var queryResults = _tableClient.QueryAsync<InventoryTableEntity>(
            filter: $"QuantityAvailable lt {threshold}",
            cancellationToken: cancellationToken);

        await foreach (var entity in queryResults.WithCancellation(cancellationToken))
        {
            yield return entity;
        }
    }
}

// Table Storage entity model
public class InventoryTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;  // ProductId
    public string RowKey { get; set; } = string.Empty;        // WarehouseId
    public DateTimeOffset? Timestamp { get; set; }            // Managed by Table Storage
    public ETag ETag { get; set; }                            // For optimistic concurrency

    // Domain properties
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
    public int QuantityAvailable { get; set; }
    public string LastUpdatedBy { get; set; } = string.Empty;
    public DateTime UpdateTimestamp { get; set; }             // Event timestamp (custom property)
    public string SchemaVersion { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;       // For deduplication
}
```

---

## Idempotency with Table Storage

**Challenge**: Prevent duplicate processing of the same Service Bus message.

### Solution 1: Last-Write-Wins (Simple)

- Use `TableUpdateMode.Replace` with `ETag.All`
- Always overwrites existing entity
- ✅ Simple, no conflicts
- ❌ May lose data if events arrive out of order

### Solution 2: EventId Deduplication (Better)

- Before upsert, check if `EventId` already exists
- If exists and same, skip processing (idempotent)
- If exists but different, update only if timestamp newer
- ✅ Handles duplicates and out-of-order events
- ❌ Requires additional read before write

### Solution 3: Optimistic Concurrency (Best)

- Read entity with ETag
- Update only if ETag matches (no changes in between)
- If conflict, re-read and retry
- ✅ Prevents lost updates
- ❌ More complex retry logic

```csharp
// Optimistic concurrency example
public async Task UpsertWithOptimisticConcurrencyAsync(
    InventoryUpdatedEvent inventoryEvent,
    CancellationToken cancellationToken)
{
    const int maxRetries = 3;
    int attempt = 0;

    while (attempt < maxRetries)
    {
        try
        {
            // Read existing entity (if exists)
            InventoryTableEntity? existing = null;
            ETag etag = ETag.All;

            try
            {
                var response = await _tableClient.GetEntityAsync<InventoryTableEntity>(
                    inventoryEvent.ProductId,
                    inventoryEvent.WarehouseId,
                    cancellationToken: cancellationToken);

                existing = response.Value;
                etag = existing.ETag;

                // Check if this event is older than existing (out-of-order)
                if (existing.UpdateTimestamp > inventoryEvent.Timestamp.UtcDateTime)
                {
                    _logger.LogWarning(
                        "Ignoring out-of-order event: EventId={EventId}, EventTime={EventTime}, ExistingTime={ExistingTime}",
                        inventoryEvent.EventId,
                        inventoryEvent.Timestamp,
                        existing.UpdateTimestamp);
                    return; // Skip this event
                }

                // Check for duplicate EventId
                if (existing.EventId == inventoryEvent.EventId.ToString())
                {
                    _logger.LogInformation(
                        "Skipping duplicate event: EventId={EventId}",
                        inventoryEvent.EventId);
                    return; // Already processed
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Entity doesn't exist, will insert with ETag.All
            }

            // Create updated entity
            var entity = new InventoryTableEntity
            {
                PartitionKey = inventoryEvent.ProductId,
                RowKey = inventoryEvent.WarehouseId,
                QuantityOnHand = inventoryEvent.QuantityOnHand,
                QuantityReserved = inventoryEvent.QuantityReserved,
                QuantityAvailable = inventoryEvent.QuantityAvailable,
                LastUpdatedBy = inventoryEvent.LastUpdatedBy,
                UpdateTimestamp = inventoryEvent.Timestamp.UtcDateTime,
                SchemaVersion = inventoryEvent.SchemaVersion,
                EventId = inventoryEvent.EventId.ToString(),
                ETag = etag
            };

            // Upsert with optimistic concurrency (will fail if ETag changed)
            await _tableClient.UpsertEntityAsync(
                entity,
                TableUpdateMode.Replace,
                cancellationToken);

            return; // Success
        }
        catch (RequestFailedException ex) when (ex.Status == 412) // Precondition Failed (ETag mismatch)
        {
            attempt++;
            _logger.LogWarning(
                "Optimistic concurrency conflict (attempt {Attempt}/{MaxRetries}), retrying...",
                attempt,
                maxRetries);

            if (attempt >= maxRetries)
            {
                throw new InvalidOperationException(
                    $"Failed to upsert after {maxRetries} attempts due to concurrency conflicts",
                    ex);
            }

            // Brief delay before retry
            await Task.Delay(100 * attempt, cancellationToken);
        }
    }
}
```

---

## Limitations and Workarounds

### Limitation 1: Complex Queries

**Problem**: Can't query "all low-stock items across all warehouses" efficiently.

**Workarounds**:

1. **Materialized View Table**: Separate `LowStockAlerts` table updated by consumer when `QuantityAvailable <= threshold`
2. **Scheduled Aggregation**: Timer-triggered function scans inventory, writes to separate table
3. **Use Cosmos DB Instead**: If complex queries are critical, Table Storage may not be suitable

### Limitation 2: Multi-Entity Transactions

**Problem**: Can't update product inventory in multiple warehouses atomically.

**Workarounds**:

1. **Batch Operations**: Only works within same PartitionKey (same product OR same warehouse)
2. **Event Sourcing**: Store all events, rebuild state from event stream
3. **Accept Eventual Consistency**: Design system to tolerate temporary inconsistencies

### Limitation 3: Aggregations

**Problem**: Can't query "total quantity across all warehouses" without full scan.

**Workarounds**:

1. **Separate Aggregate Table**: Maintain `ProductTotals` table with computed aggregates
2. **Read-side Projection**: Consumer updates both detail and aggregate tables
3. **Periodic Recalculation**: Scheduled job computes aggregates

---

## When to Use Table Storage vs Cosmos DB

### Use Azure Table Storage when:

- ✅ Query patterns are simple (point reads, single-partition queries)
- ✅ Cost is primary concern (10-20x cheaper)
- ✅ Data model is flat (no nested objects, arrays)
- ✅ CRM queries are predictable and key-based
- ✅ Write-heavy workload with simple reads
- ✅ Throughput requirements are moderate (<20K ops/sec per table)

### Use Cosmos DB when:

- ✅ Complex queries required (filters, sorts, aggregations)
- ✅ Global distribution needed
- ✅ Sub-10ms latency critical
- ✅ Rich data model (nested JSON documents)
- ✅ Schema evolution important
- ✅ Change feed / event sourcing patterns
- ✅ Multi-region writes required
- ✅ Higher throughput (100K+ RU/s)

---

## Recommended Hybrid Approach

For this architecture, consider a **hybrid approach**:

1. **Table Storage for current state**: Fast, cheap, key-based queries for CRM
2. **Cosmos DB change feed for analytics**: Rich querying for business intelligence
3. **Blob Storage for event archive**: Long-term event history retention

**Example**:

- Consumer writes to Table Storage (primary data store for CRM)
- Consumer also appends events to Blob Storage (audit trail)
- Separate analytics pipeline reads from Blob Storage → Cosmos DB for BI queries

This gives you the cost benefits of Table Storage while retaining rich query capabilities where needed.

---

## DI Registration and Configuration

### Dependency Injection Setup (Program.cs)

```csharp
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using InventoryConsumer.Services;

var builder = WebApplication.CreateBuilder(args);

// Register Azure Table Storage client
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var storageAccountName = config["TableStorage:AccountName"]
        ?? throw new InvalidOperationException("TableStorage:AccountName not configured");

    var tableServiceUri = new Uri($"https://{storageAccountName}.table.core.windows.net");

    return new TableServiceClient(
        tableServiceUri,
        new DefaultAzureCredential());
});

// Register Azure Service Bus client
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var serviceBusNamespace = config["ServiceBus:Namespace"]
        ?? throw new InvalidOperationException("ServiceBus:Namespace not configured");

    return new ServiceBusClient(
        $"{serviceBusNamespace}.servicebus.windows.net",
        new DefaultAzureCredential());
});

// Register application services
builder.Services.AddSingleton<IInventoryRepository, TableStorageInventoryRepository>();

// Register background Service Bus processor
builder.Services.AddHostedService<InventoryMessageProcessor>();

var app = builder.Build();
app.Run();
```

### Configuration (appsettings.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ServiceBus": {
    "Namespace": "asi-servicebus-prod",
    "TopicName": "inventory-updated",
    "SubscriptionName": "nosql-writer-inventory"
  },
  "TableStorage": {
    "AccountName": "asitablestorageprod",
    "InventoryTableName": "InventoryItems"
  }
}
```

### Azure RBAC Configuration

Grant the consumer application's Managed Identity these roles:

- **Storage Table Data Contributor** on the storage account
- **Azure Service Bus Data Receiver** on the Service Bus namespace

```bash
# Grant Table Storage access
az role assignment create \
  --assignee <managed-identity-principal-id> \
  --role "Storage Table Data Contributor" \
  --scope /subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.Storage/storageAccounts/<account-name>

# Grant Service Bus access
az role assignment create \
  --assignee <managed-identity-principal-id> \
  --role "Azure Service Bus Data Receiver" \
  --scope /subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.ServiceBus/namespaces/<namespace-name>
```

---

## Performance Considerations

### Azure Table Storage Limits

- **Throughput**: 20,000 operations/sec per table (can scale higher with partitioning)
- **Entity Size**: 1 MB max
- **Properties**: 255 max per entity
- **Property Size**: 64 KB max for string properties
- **Latency**: Typically 10-50ms for single-partition queries

### Optimization Tips

1. **Partition Key Design**: Choose high-cardinality keys for even distribution
2. **Minimize Cross-Partition Queries**: Design schema around query patterns
3. **Use Batch Operations**: Up to 100 operations in same partition
4. **Enable CDN/Caching**: For read-heavy workloads
5. **Monitor Throttling**: Watch for 503 responses, implement exponential backoff
6. **Use Async/Await**: Properly leverage async operations for better throughput

---

## Summary

Azure Table Storage provides a cost-effective solution for storing inventory data from Service Bus messages when query patterns are simple and predictable. The key to success is:

1. **Choose the right partition strategy** based on CRM query patterns
2. **Implement proper idempotency** to handle duplicate messages
3. **Understand limitations** and plan workarounds for complex queries
4. **Consider hybrid approaches** when you need both cost savings and rich querying

For the `inventory-updated` topic, the **product-centric strategy** (PartitionKey = ProductId, RowKey = WarehouseId) is recommended if CRM primarily queries inventory by product. If warehouse-based queries are more common, flip the keys accordingly.
