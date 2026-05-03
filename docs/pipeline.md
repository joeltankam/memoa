# Pipeline

The pipeline controls how captured requests are delivered from the middleware to the registered sinks.
Memoa offers two pipeline modes to balance latency impact and delivery guarantees.

## Pipeline Modes

### Background (default)

Captured requests are written to a bounded `Channel<T>` and processed by background workers.
This minimizes the impact on HTTP request latency.

```csharp
builder.Services.AddMemoa(options =>
{
    options.Pipeline.Mode = PipelineMode.Background;
    options.Pipeline.ChannelCapacity = 2048;
    options.Pipeline.WorkerCount = 2;
});
```

**Characteristics:**

- Near-zero latency impact on HTTP responses
- Requests may be dropped if the channel fills up (configurable)
- Delivery is best-effort during application shutdown (drains within `ShutdownTimeout`)
- Ordering is preserved per-worker

### Inline

Captured requests are written to all sinks synchronously before the response is returned to the client.

```csharp
builder.Services.AddMemoa(options =>
{
    options.Pipeline.Mode = PipelineMode.Inline;
});
```

**Characteristics:**

- Guaranteed delivery (write happens before response)
- Adds latency to every captured request (depends on sink performance)
- Simpler model — no background workers
- Suitable for low-throughput scenarios or when durability is critical

## Channel Full Behavior

When the background channel is full, the `FullMode` setting determines what happens:

| Mode | Behavior |
|------|----------|
| `DropWrite` (default) | The new request is discarded; a metric is emitted |
| `DropOldest` | The oldest request in the channel is discarded to make room |
| `Wait` | The middleware blocks until space is available (use with caution) |

```json
{
  "Memoa": {
    "Pipeline": {
      "FullMode": "DropWrite",
      "ChannelCapacity": 4096
    }
  }
}
```

## Worker Count

Multiple background workers can process the channel concurrently for higher throughput:

```csharp
options.Pipeline.WorkerCount = 4;
```

Each worker independently reads from the channel and writes to all registered sinks.
Ordering across workers is not guaranteed when `WorkerCount > 1`.

## Shutdown Behavior

On application shutdown:

1. The channel writer is completed (no new requests accepted)
2. Workers continue draining remaining items
3. If not drained within `ShutdownTimeout`, processing stops
4. The `CancellationToken` is signaled for graceful worker termination

```json
{
  "Memoa": {
    "Pipeline": {
      "ShutdownTimeout": "00:01:00"
    }
  }
}
```

## Tuning Recommendations

| Scenario | Recommended Settings |
|----------|---------------------|
| Low-traffic API | `Background`, capacity 256, 1 worker |
| High-traffic API | `Background`, capacity 4096, 2-4 workers |
| Must-capture (auditing) | `Inline` or `Background` with `FullMode: Wait` |
| Development/debugging | `Inline` (simplest) |

## Metrics

The pipeline emits OpenTelemetry metrics to help you tune:

- `memoa.channel.queue_size` — Current channel depth (NET 7+)
- `memoa.requests.dropped` — Requests dropped due to full channel
- `memoa.requests.captured` — Total captured requests
- `memoa.sink.write.duration` — Histogram of sink write times

See [Observability](observability.md) for details.
