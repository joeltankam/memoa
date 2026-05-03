# Memoa

ASP.NET Core middleware that captures and persists HTTP requests for review and replay.

## Packages

| Package | Description |
|---------|-------------|
| `Memoa.Core` | Core middleware, abstractions, and pipeline |
| `Memoa.Sinks.AzureBlobStorage` | Azure Blob Storage sink (write & read) |
| `Memoa.Replay.Cli` | .NET tool to replay captured requests |

## Quick Start

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddMemoa(opts =>
    {
        opts.Capture.IncludeHeaders = true;
        opts.Capture.IncludeBody = true;
        opts.Pipeline.Mode = PipelineMode.Background;
    })
    .WriteTo.AzureBlobStorage("UseDevelopmentStorage=true");

var app = builder.Build();
app.UseMemoa();
// ... your routes
app.Run();
```

## Configuration

Bind from `appsettings.json`:

```json
{
  "Memoa": {
    "Enabled": true,
    "CorrelationIdHeader": "X-Correlation-Id",
    "Capture": {
      "IncludeHeaders": true,
      "IncludeBody": true,
      "IncludeResponse": false,
      "MaxBodySizeBytes": 1048576,
      "HeaderDenyList": ["Authorization", "Cookie", "Set-Cookie"]
    },
    "Filters": {
      "PathIncludePatterns": ["/**"],
      "PathExcludePatterns": ["/health*", "/metrics*", "/favicon.ico"],
      "Methods": ["GET", "POST", "PUT", "PATCH", "DELETE"]
    },
    "Pipeline": {
      "Mode": "Background",
      "ChannelCapacity": 1024,
      "WorkerCount": 1,
      "FullMode": "DropWrite"
    }
  }
}
```

## Replay CLI

Install as a .NET tool:

```bash
dotnet tool install --global Memoa.Replay.Cli
```

Replay captured requests:

```bash
memoa-replay \
  --connection-string "UseDevelopmentStorage=true" \
  --target https://localhost:5001 \
  --from "2024-01-01T00:00:00Z" \
  --methods GET POST \
  --dry-run
```

## OpenTelemetry

Memoa emits traces and metrics via `System.Diagnostics`:

- **ActivitySource**: `"Memoa"` — spans for `memoa.capture` and `memoa.sink.write`
- **Meter**: `"Memoa"` — counters for captured/dropped/written/failed/skipped requests, histogram for sink write duration, gauge for channel queue size

## Architecture

```
HTTP Request → MemoaMiddleware → IRequestPipeline → IRequestSink(s)
                                       ↓
                          BackgroundRequestPipeline (Channel<T>)
                                  or
                          InlineRequestPipeline (sync)
```

## Building

```bash
dotnet build
dotnet test --filter "TestCategory!=Azurite"
```

Integration tests require [Azurite](https://github.com/Azure/Azurite):

```bash
docker run -p 10000:10000 mcr.microsoft.com/azure-storage/azurite
dotnet test
```

## License

MIT
