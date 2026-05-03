# Memoa

ASP.NET Core middleware that captures and persists HTTP requests for review and replay.

## Packages

| Package | Description |
|---------|-------------|
| `Memoa.Core` | Core middleware, abstractions, and pipeline |
| `Memoa.Sinks.File` | Local file system sink |
| `Memoa.Sinks.AzureBlobStorage` | Azure Blob Storage sink |
| `Memoa.Sinks.AmazonS3` | Amazon S3 / S3-compatible sink |
| `Memoa.Sinks.Redis` | Redis Streams sink |
| `Memoa.Replay.Core` | Shared replay engine (timeline, parallelism) |
| `Memoa.Replay.Cli` | .NET global tool for request replay |
| `Memoa.Replay.Api` | REST API endpoints for in-app replay |

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
    },
    "Sampling": {
      "Rate": 1.0
    }
  }
}
```

## Replay CLI

Install as a .NET tool:

```bash
dotnet tool install --global Memoa.Replay.Cli
```

Replay captured requests from any source with optional timeline reproduction:

```bash
memoa-replay \
  --source azure \
  --connection-string "UseDevelopmentStorage=true" \
  --target https://localhost:5001 \
  --timeline relative \
  --from "2026-05-01T00:00:00Z" \
  --methods GET POST
```

Supported sources: `azure`, `file`, `s3`, `redis`.

## Replay API

Inject replay REST endpoints into your ASP.NET Core application:

```csharp
builder.Services.AddMemoaReplay(options =>
{
    options.RoutePrefix = "/replay";
    options.TargetBaseUrl = "https://staging.api.example.com";
    options.AuthorizationPolicy = "AdminOnly";
});

app.MapMemoaReplay();
```

Endpoints: `GET /replay` (query requests), `POST /replay/run` (fire-and-forget replay),
`GET /replay/jobs/{id}` (poll status).

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
