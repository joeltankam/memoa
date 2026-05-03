# Architecture

This document describes the internal architecture of Memoa, its extension points,
and design decisions.

## High-Level Flow

```
HTTP Request
     │
     ▼
┌─────────────────┐
│ MemoaMiddleware  │  ← Registered via app.UseMemoa()
└────────┬────────┘
         │
    ┌────▼────┐
    │ Filters │  PathFilter, HeaderFilter, ContentTypeClassifier
    └────┬────┘
         │ (passes filter?)
         ▼
┌─────────────────┐
│ IRequestPipeline │
└────────┬────────┘
         │
    ┌────┴────────────────────────┐
    │                             │
    ▼                             ▼
┌──────────────────┐    ┌───────────────────────────┐
│ InlinePipeline   │    │ BackgroundPipeline         │
│ (sync write)     │    │ (Channel<T> + workers)     │
└────────┬─────────┘    └──────────┬────────────────┘
         │                         │
         ▼                         ▼
┌─────────────────────────────────────┐
│          IRequestSink(s)            │
│  File │ Azure Blob │ S3 │ Redis    │
└─────────────────────────────────────┘
```

## Key Components

### MemoaMiddleware

- **Namespace:** `Memoa.Internal`
- **Visibility:** `internal` (not exposed to consumers)
- Intercepts every HTTP request
- Evaluates filters (path, method, enabled)
- Captures request/response data according to `MemoaCaptureOptions`
- Submits `RecordedRequest` to the pipeline
- Uses `MemoaDiagnostics.ActivitySource` for tracing

### IRequestPipeline

```csharp
internal interface IRequestPipeline
{
    ValueTask SubmitAsync(RecordedRequest request, CancellationToken cancellationToken);
}
```

Two implementations:

- **InlineRequestPipeline** — Writes to all sinks synchronously
- **BackgroundRequestPipeline** — Writes to a `Channel<RecordedRequest>`, consumed by workers

### IRequestSink

```csharp
public interface IRequestSink
{
    ValueTask WriteAsync(RecordedRequest request, CancellationToken cancellationToken);
}
```

The primary extension point. All sink packages implement this interface.

### IRequestSource

```csharp
public interface IRequestSource
{
    IAsyncEnumerable<RecordedRequest> ReadAsync(RequestQuery query, CancellationToken cancellationToken);
}
```

Optional interface for sinks that support reading back captured requests (all built-in sinks do).

## Configuration Flow

```
IConfiguration ("Memoa" section)
        │
        ▼
┌─────────────────┐
│   MemoaOptions   │ ← IOptions<MemoaOptions>
├─────────────────┤
│ CaptureOptions   │
│ FilterOptions    │
│ PipelineOptions  │
└─────────────────┘
        │
        ▼
  Service Registration
  (Pipeline, Sinks, Middleware)
```

## Registration Architecture

```csharp
services.AddMemoa(configuration)   // Returns IMemoaBuilder
    .WriteTo                        // Returns MemoaSinkBuilder
        .FileSystem(...)            // Extension method on MemoaSinkBuilder
        .Redis(...);                // Chainable — registers additional IRequestSink
```

- `IMemoaBuilder` — Orchestrates core service registration
- `MemoaSinkBuilder` — Fluent builder that sink extension methods target
- Both expose `IServiceCollection` for DI registration
- `MemoaSinkBuilder.Configuration` provides the `"Sinks"` sub-section for config binding

## Internal Types

All internal implementation types live in the `Memoa.Internal` namespace:

| Type | Responsibility |
|------|---------------|
| `MemoaMiddleware` | ASP.NET Core middleware |
| `MemoaBuilder` | `IMemoaBuilder` implementation |
| `InlineRequestPipeline` | Sync pipeline |
| `BackgroundRequestPipeline` | Async pipeline (also `IHostedService`) |
| `MemoaDiagnostics` | Static ActivitySource + Meter |
| `GlobMatcher` | Path pattern matching |
| `HeaderFilter` | Header allow/deny list evaluation |
| `PathFilter` | Path include/exclude evaluation |
| `ContentTypeClassifier` | Determines text vs binary body |
| `ResponseCaptureStream` | Stream wrapper for response body capture |

## InternalsVisibleTo

Test projects and Moq's `DynamicProxyGenAssembly2` are granted internal access via
`Directory.Build.targets`:

```xml
<InternalsVisibleTo Include="$(AssemblyName).Tests" />
<InternalsVisibleTo Include="DynamicProxyGenAssembly2" />
```

## Threading Model

- **Background mode:** Single-writer channel, N-reader workers
- Workers run as `Task.Run` managed by `BackgroundRequestPipeline` (IHostedService)
- Sink writes are serialized per-worker (no concurrent writes to the same sink from one worker)
- Multiple workers may write to the same sink concurrently — sinks must be thread-safe

## NuGet Package Dependencies

```text
Memoa.Core
├── FrameworkReference: Microsoft.AspNetCore.App
└── (no external packages)

Memoa.Sinks.File
└── ProjectReference: Memoa.Core

Memoa.Sinks.AzureBlobStorage
├── Azure.Storage.Blobs
├── Microsoft.Extensions.Azure
└── ProjectReference: Memoa.Core

Memoa.Sinks.AmazonS3
├── AWSSDK.S3
└── ProjectReference: Memoa.Core

Memoa.Sinks.Redis
├── StackExchange.Redis
└── ProjectReference: Memoa.Core

Memoa.Replay.Core
├── Microsoft.Extensions.Http
├── Microsoft.Extensions.Logging.Abstractions
└── ProjectReference: Memoa.Core

Memoa.Replay.Cli
├── System.CommandLine
├── ProjectReference: Memoa.Core
├── ProjectReference: Memoa.Replay.Core
├── ProjectReference: Memoa.Sinks.AzureBlobStorage
├── ProjectReference: Memoa.Sinks.File
├── ProjectReference: Memoa.Sinks.AmazonS3
└── ProjectReference: Memoa.Sinks.Redis

Memoa.Replay.Api
├── FrameworkReference: Microsoft.AspNetCore.App
├── ProjectReference: Memoa.Core
└── ProjectReference: Memoa.Replay.Core
```
