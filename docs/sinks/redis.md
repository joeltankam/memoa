# Redis Sink

The Redis sink appends captured requests to a Redis Stream using StackExchange.Redis.
Ideal for real-time consumption, short-lived retention, and streaming architectures.

## Installation

```bash
dotnet add package Memoa.Sinks.Redis
```

## Registration

### With connection string

```csharp
builder.Services
    .AddMemoa()
    .WriteTo.Redis("localhost:6379");
```

### With options

```csharp
builder.Services
    .AddMemoa()
    .WriteTo.Redis("localhost:6379", options =>
    {
        options.StreamKey = "my-app:captured-requests";
        options.MaxLength = 50_000;
        options.Database = 2;
    });
```

### With pre-registered IConnectionMultiplexer

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379,abortConnect=false"));

builder.Services
    .AddMemoa()
    .WriteTo.Redis(options =>
    {
        options.StreamKey = "memoa:requests";
    });
```

### From configuration

```csharp
builder.Services
    .AddMemoa(config.GetSection("Memoa"))
    .WriteTo.Redis(config.GetSection("Memoa:Sinks:Redis"));
```

## Configuration Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ConnectionString` | `string?` | `null` | Redis connection string (ignored if `IConnectionMultiplexer` is pre-registered) |
| `StreamKey` | `string` | `"memoa:requests"` | Redis Stream key name |
| `MaxLength` | `int?` | `10000` | Maximum stream entries (MAXLEN ~); `null` for unlimited |
| `Database` | `int` | `-1` | Redis database index (`-1` = default) |
| `KeyPrefix` | `string?` | `null` | Optional prefix prepended to stream key |

### appsettings.json example

```json
{
  "Memoa": {
    "Sinks": {
      "Redis": {
        "ConnectionString": "localhost:6379,abortConnect=false",
        "StreamKey": "memoa:requests",
        "MaxLength": 10000,
        "Database": 0,
        "KeyPrefix": "myapp"
      }
    }
  }
}
```

## Stream Entry Format

Each captured request is stored as a stream entry with these fields:

| Field | Content |
|-------|---------|
| `id` | Request GUID |
| `timestamp` | Unix milliseconds |
| `method` | HTTP method |
| `path` | Request path |
| `data` | Full JSON serialized `RecordedRequest` |

## Reading Back

The Redis sink implements `IRequestSource`. Reading uses `XRANGE` with timestamp-based filtering:

```csharp
var source = serviceProvider.GetRequiredService<RedisSink>();
var query = new RequestQuery
{
    From = DateTimeOffset.UtcNow.AddHours(-1),
    Methods = new[] { "POST", "PUT" }
};

await foreach (var request in source.ReadAsync(query, ct))
{
    Console.WriteLine($"{request.Method} {request.Path}");
}
```

## Consumer Groups

While Memoa writes to a plain stream, you can attach Redis consumer groups for
downstream processing:

```bash
redis-cli XGROUP CREATE memoa:requests mygroup $ MKSTREAM
redis-cli XREADGROUP GROUP mygroup consumer1 COUNT 10 BLOCK 5000 STREAMS memoa:requests >
```

## Retention

The `MaxLength` option uses Redis's approximate trimming (`MAXLEN ~`) to keep the stream
bounded. For time-based retention, combine with a scheduled task or Redis expiry policies.

## Performance Considerations

- Uses `XADD` with `MAXLEN ~` for bounded O(1) appends
- Connection is shared across all writes (singleton `IConnectionMultiplexer`)
- For high-throughput, the Background pipeline mode batches writes effectively
- Consider `abortConnect=false` in the connection string for resilient connections
