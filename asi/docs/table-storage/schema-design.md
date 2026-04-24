# Table Storage Schema Design

## Overview

Azure Table Storage serves as the consolidated NoSQL data store that feeds Dynamics CRM. This document defines the table structure, partition strategies, and entity schemas for each domain.

## Table Inventory

| Table Name           | Domain    | Partition Strategy     | Estimated Size | TTL Policy |
| -------------------- | --------- | ---------------------- | -------------- | ---------- |
| `InventoryItems`     | Inventory | locationId             | 1M-10M rows    | None       |
| `InventoryTransfers` | Inventory | transferDate (YYYY-MM) | 100K-1M rows   | 2 years    |
| `Orders`             | Orders    | customerId             | 1M-100M rows   | None       |
| `OrderLineItems`     | Orders    | orderId                | 10M-1B rows    | None       |
| `OrderPayments`      | Orders    | orderId                | 1M-100M rows   | 7 years    |
| `Shipments`          | Orders    | shipmentDate (YYYY-MM) | 1M-100M rows   | 2 years    |
| `Customers`          | Customers | regionCode             | 100K-10M rows  | None       |
| `CustomerAddresses`  | Customers | customerId             | 200K-20M rows  | None       |
| `Products`           | Products  | categoryId             | 10K-1M rows    | None       |
| `Prices`             | Pricing   | productId              | 10K-1M rows    | None       |

## Partition Key Strategies

### Option 1: High Cardinality Key (Even Distribution)

```
PartitionKey = customerId
RowKey = orderId
```

**Pros:**

- Even distribution across partitions
- Excellent scalability

**Cons:**

- Queries across customers require cross-partition queries

### Option 2: Low Cardinality Key (Query Optimization)

```
PartitionKey = regionCode (e.g., "EAST", "WEST")
RowKey = customerId
```

**Pros:**

- Efficient regional queries
- CRM often queries by region

**Cons:**

- Potential hot partitions if regions are uneven
- Limited scalability within region

### Option 3: Time-Based Partitioning

```
PartitionKey = YYYY-MM
RowKey = {entityId}
```

**Pros:**

- Efficient time-range queries
- Easy to archive old partitions

**Cons:**

- Hot partition for current month
- Requires partition per time period

### Recommended Strategy by Table

| Table          | Partition Key       | Row Key           | Rationale                         |
| -------------- | ------------------- | ----------------- | --------------------------------- |
| InventoryItems | `locationId`        | `inventoryItemId` | CRM queries inventory by location |
| Orders         | `customerId`        | `orderId`         | CRM queries orders by customer    |
| OrderLineItems | `orderId`           | `lineNumber`      | Always queried with parent order  |
| Customers      | `{customerId[0:2]}` | `customerId`      | Even distribution, deterministic  |
| Products       | `categoryId`        | `productId`       | CRM browses products by category  |

## Entity Schemas

### InventoryItems Table

```csharp
public class InventoryItemEntity : ITableEntity
{
    // Azure Table Storage required properties
    public string PartitionKey { get; set; }  // locationId
    public string RowKey { get; set; }        // inventoryItemId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // Business properties
    public string InventoryItemId { get; set; }
    public string Sku { get; set; }
    public string Upc { get; set; }
    public string ProductId { get; set; }
    public string LocationId { get; set; }
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
    public int QuantityAvailable { get; set; }
    public int ReorderPoint { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime? LastCountedDate { get; set; }
    public DateTime? LastReceivedDate { get; set; }
    public string BinLocation { get; set; }
    public string Status { get; set; }

    // Audit fields
    public DateTime LastUpdated { get; set; }
    public string LastUpdatedBy { get; set; }
    public string CorrelationId { get; set; }
}
```

### Orders Table

```csharp
public class OrderEntity : ITableEntity
{
    // Azure Table Storage required properties
    public string PartitionKey { get; set; }  // customerId
    public string RowKey { get; set; }        // orderId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // Business properties
    public string OrderId { get; set; }
    public string OrderNumber { get; set; }
    public string CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public string OrderType { get; set; }
    public string Channel { get; set; }
    public string Status { get; set; }
    public string PaymentStatus { get; set; }
    public string FulfillmentStatus { get; set; }

    // Monetary amounts
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; }

    // Addresses (JSON serialized)
    public string BillingAddressJson { get; set; }
    public string ShippingAddressJson { get; set; }

    // Shipping
    public string ShippingMethod { get; set; }
    public string TrackingNumber { get; set; }
    public DateTime? EstimatedDeliveryDate { get; set; }
    public DateTime? ActualDeliveryDate { get; set; }

    // Audit
    public string SourceSystem { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime LastModifiedDate { get; set; }
    public string CorrelationId { get; set; }
}
```

### Customers Table

```csharp
public class CustomerEntity : ITableEntity
{
    // Azure Table Storage required properties
    public string PartitionKey { get; set; }  // First 2 chars of customerId
    public string RowKey { get; set; }        // customerId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // Business properties
    public string CustomerId { get; set; }
    public string CustomerNumber { get; set; }
    public string CustomerType { get; set; }
    public string Status { get; set; }

    // Personal information
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string CompanyName { get; set; }
    public string Email { get; set; }
    public bool EmailVerified { get; set; }
    public string Phone { get; set; }
    public string PhoneType { get; set; }

    // Customer metrics
    public decimal TotalLifetimeValue { get; set; }
    public int TotalOrders { get; set; }
    public decimal AverageOrderValue { get; set; }
    public string LoyaltyTier { get; set; }
    public int LoyaltyPoints { get; set; }

    // Preferences
    public bool MarketingOptIn { get; set; }
    public bool SmsOptIn { get; set; }
    public string Language { get; set; }

    // Audit
    public DateTime CreatedDate { get; set; }
    public DateTime LastModifiedDate { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public string CorrelationId { get; set; }
}
```

### Products Table

```csharp
public class ProductEntity : ITableEntity
{
    // Azure Table Storage required properties
    public string PartitionKey { get; set; }  // categoryId
    public string RowKey { get; set; }        // productId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // Business properties
    public string ProductId { get; set; }
    public string Sku { get; set; }
    public string Upc { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string CategoryId { get; set; }
    public string CategoryName { get; set; }
    public string Brand { get; set; }
    public string Manufacturer { get; set; }

    // Pricing (denormalized for convenience)
    public decimal ListPrice { get; set; }
    public decimal SalePrice { get; set; }
    public string Currency { get; set; }

    // Inventory (aggregated)
    public int TotalQuantityOnHand { get; set; }
    public int TotalQuantityAvailable { get; set; }

    // Attributes (JSON serialized)
    public string AttributesJson { get; set; }

    // Media
    public string PrimaryImageUrl { get; set; }
    public string ImageUrlsJson { get; set; }  // JSON array

    // Status
    public string Status { get; set; }
    public bool IsActive { get; set; }

    // Audit
    public DateTime CreatedDate { get; set; }
    public DateTime LastModifiedDate { get; set; }
    public string CorrelationId { get; set; }
}
```

## Denormalization Strategies

### Embedded JSON for Complex Objects

```csharp
// Instead of separate AddressEntity table entries:
public string BillingAddressJson { get; set; }

// Stored as:
{
  "street": "123 Main St",
  "city": "Seattle",
  "state": "WA",
  "zip": "98101",
  "country": "USA"
}
```

**When to use:**

- 1:1 relationships
- Always queried together
- Small data size

### Separate Tables for 1:Many

```csharp
// OrderLineItems in separate table
// PartitionKey = orderId
// RowKey = lineNumber

// Allows efficient queries:
// "Get all line items for order ORD-12345"
```

**When to use:**

- 1:Many relationships
- May be queried independently
- Unbounded collections

### Denormalized Aggregates

```csharp
// On CustomerEntity:
public decimal TotalLifetimeValue { get; set; }
public int TotalOrders { get; set; }

// Updated by consumer when processing order events
```

**When to use:**

- Frequently accessed aggregates
- Acceptable to be eventually consistent
- Avoids expensive calculations at query time

## Indexing Strategy

Azure Table Storage automatically indexes:

- PartitionKey
- RowKey
- Timestamp

### Secondary Index Pattern

Create additional tables for alternate query patterns:

```csharp
// Primary table: Customers
// PartitionKey = {customerId[0:2]}
// RowKey = customerId

// Secondary index: CustomersByEmail
// PartitionKey = {email[0:2]}
// RowKey = email
// CustomerId property = customerId (reference to primary)
```

### Composite Key Pattern

Embed lookup values in RowKey:

```csharp
// PartitionKey = locationId
// RowKey = {sku}#{inventoryItemId}

// Enables queries like:
// "Find all inventory items for SKU 'WIDGET-001' at location 'WAREHOUSE-01'"
```

## Query Patterns for CRM Integration

### Pattern 1: Point Query (Fastest)

```csharp
// Get specific customer
var customer = await tableClient.GetEntityAsync<CustomerEntity>(
    partitionKey: "CU",
    rowKey: "CUST-12345");
```

### Pattern 2: Partition Query (Fast)

```csharp
// Get all inventory at a location
var items = tableClient.QueryAsync<InventoryItemEntity>(
    filter: $"PartitionKey eq 'WAREHOUSE-01'");
```

### Pattern 3: Cross-Partition Query (Slow, Avoid if Possible)

```csharp
// Get all orders with status 'Pending'
var orders = tableClient.QueryAsync<OrderEntity>(
    filter: $"Status eq 'Pending'");
// This scans ALL partitions!
```

### Pattern 4: Top N with Filter

```csharp
// Get recent 100 orders for customer
var recentOrders = tableClient.QueryAsync<OrderEntity>(
    filter: $"PartitionKey eq '{customerId}'")
    .OrderByDescending(o => o.OrderDate)
    .Take(100);
```

## Schema Evolution

### Adding Fields (Safe)

```csharp
// Add new optional property
public string NewField { get; set; }

// Old entities will have null/default value
// No migration required
```

### Removing Fields (Risky)

```csharp
// Remove property from code
// public string OldField { get; set; }  // REMOVED

// Data still exists in Table Storage
// Consider setting to null during write
```

### Renaming Fields (Complex)

1. Add new field
2. Populate both fields during writes
3. Backfill old data
4. Update all readers
5. Remove old field

## Performance Optimization

### Batch Operations

```csharp
// Insert/update up to 100 entities in one transaction
// All must have same PartitionKey
var batch = new List<TableTransactionAction>();

foreach (var lineItem in orderLineItems)
{
    batch.Add(new TableTransactionAction(
        TableTransactionActionType.UpsertReplace,
        lineItem));
}

await tableClient.SubmitTransactionAsync(batch);
```

### Pagination

```csharp
await foreach (Page<OrderEntity> page in tableClient.QueryAsync<OrderEntity>()
    .AsPages(pageSizeHint: 1000))
{
    foreach (OrderEntity order in page.Values)
    {
        // Process order
    }
}
```

### Select Specific Properties

```csharp
// Only retrieve needed properties
var customers = tableClient.QueryAsync<CustomerEntity>(
    select: new[] { "CustomerId", "Email", "FirstName", "LastName" });
```

## Storage Account Configuration

### Performance Tier

- **Standard (HDD)** - Low cost, slower (~100 requests/sec per partition)
- **Premium (SSD)** - Higher cost, faster (~2000 requests/sec per partition)

**Recommendation:** Start with Standard, upgrade to Premium if needed

### Geo-Replication

- **LRS (Locally Redundant)** - 3 copies in one region
- **ZRS (Zone Redundant)** - 3 copies across availability zones
- **GRS (Geo Redundant)** - 6 copies across two regions
- **GZRS (Geo-Zone Redundant)** - Best availability

**Recommendation:** ZRS for production (balance cost and availability)

### Monitoring

Track these metrics:

- **Total Requests** - requests/second
- **Throttling Errors** - 503 responses
- **Success Rate** - % of successful requests
- **Average E2E Latency** - end-to-end latency
- **Capacity** - total storage used

```kusto
// Application Insights query for Table Storage throttling
dependencies
| where target contains "table.core.windows.net"
| where resultCode == "503"
| summarize ThrottleCount = count() by bin(timestamp, 5m), name
```

## Data Migration

### Initial Load

```csharp
// Batch load from LOB systems
var batch = new List<CustomerEntity>();
const int batchSize = 100;

foreach (var customer in lobCustomers)
{
    batch.Add(MapToTableEntity(customer));

    if (batch.Count >= batchSize)
    {
        await BulkInsertAsync(batch);
        batch.Clear();
    }
}
```

### Continuous Sync

Kafka consumers handle ongoing synchronization automatically.

## Best Practices Summary

| Practice                       | Description                           |
| ------------------------------ | ------------------------------------- |
| **Partition Key Design**       | Choose based on CRM query patterns    |
| **Avoid Hot Partitions**       | Use high cardinality keys             |
| **Denormalize for Reads**      | Optimize for query performance        |
| **Batch Operations**           | Use transactions for related entities |
| **Select Specific Properties** | Don't retrieve unnecessary data       |
| **Monitor Throttling**         | Scale or optimize if seeing 503s      |
| **Use ETags**                  | Implement optimistic concurrency      |
| **JSON for Complex Data**      | Serialize nested objects              |
