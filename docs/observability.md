# Observability

Memoa integrates with the .NET OpenTelemetry ecosystem using `System.Diagnostics` primitives.
No additional dependencies are required.

## ActivitySource (Distributed Tracing)

**Source name:** `"Memoa"`

### Activities

| Activity | Description | Tags |
|----------|-------------|------|
| `memoa.capture` | Spans the full request capture lifecycle | `http.method`, `http.route`, `memoa.request_id` |
| `memoa.sink.write` | Spans a single sink write operation | `memoa.sink_type`, `memoa.request_id` |

### Subscribing to Traces

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Memoa");
    });
```

## Meter (Metrics)

**Meter name:** `"Memoa"`

### Instruments

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `memoa.requests.captured` | Counter | — | Total requests captured by the middleware |
| `memoa.requests.skipped` | Counter | — | Requests skipped by filters |
| `memoa.requests.dropped` | Counter | — | Requests dropped (channel full) |
| `memoa.requests.written` | Counter | — | Requests successfully written to sinks |
| `memoa.requests.failed` | Counter | — | Requests that failed to write to sinks |
| `memoa.sink.write.duration` | Histogram | ms | Duration of individual sink write operations |
| `memoa.channel.queue_size` | UpDownCounter | — | Current background channel depth (.NET 7+) |

### Subscribing to Metrics

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Memoa");
    });
```

## Diagnostics Logging

Memoa uses `Microsoft.Extensions.Logging` with structured log messages:

| Level | Category | Example |
|-------|----------|---------|
| Debug | `Memoa.*Sink` | "Wrote captured request {RequestId} to {Target}" |
| Warning | `Memoa.Internal.BackgroundRequestPipeline` | "Dropped captured request {RequestId} due to full channel" |
| Warning | `Memoa.*Sink` | "Failed to deserialize file {FilePath}, skipping" |
| Information | `Memoa.Internal.BackgroundRequestPipeline` | "Starting {WorkerCount} Memoa background pipeline worker(s)" |
| Error | `Memoa.Internal.BackgroundRequestPipeline` | "Error writing request {RequestId} to sink {SinkType}" |

### Configuring Log Levels

```json
{
  "Logging": {
    "LogLevel": {
      "Memoa": "Information",
      "Memoa.Internal": "Warning"
    }
  }
}
```

## Health Monitoring Dashboard

Key metrics for monitoring Memoa health:

1. **`memoa.requests.dropped` rate** — If non-zero, increase `ChannelCapacity` or `WorkerCount`
2. **`memoa.requests.failed` rate** — Indicates sink connectivity issues
3. **`memoa.sink.write.duration` p99** — Monitor for slow sinks affecting throughput
4. **`memoa.channel.queue_size`** — Sustained high values indicate workers can't keep up

## Grafana Dashboard Example

```promql
# Capture rate
rate(memoa_requests_captured_total[5m])

# Drop rate (should be 0)
rate(memoa_requests_dropped_total[5m])

# Sink write latency p99
histogram_quantile(0.99, rate(memoa_sink_write_duration_bucket[5m]))

# Channel saturation
memoa_channel_queue_size / on() group_left memoa_pipeline_channel_capacity
```

## Exporter Compatibility

Memoa's instrumentation is compatible with any OpenTelemetry exporter:

- OTLP (Jaeger, Tempo, etc.)
- Prometheus
- Azure Monitor / Application Insights
- AWS X-Ray
- Datadog
