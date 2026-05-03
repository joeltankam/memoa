# Configuration Reference

Memoa is configured through the `MemoaOptions` class, which can be bound from the `"Memoa"` section
in your application configuration.

## Registration Methods

### Programmatic configuration

```csharp
builder.Services.AddMemoa(options =>
{
    options.Enabled = true;
    options.Capture.IncludeBody = true;
    options.Pipeline.Mode = PipelineMode.Background;
});
```

### Configuration binding

```csharp
builder.Services.AddMemoa(builder.Configuration.GetSection("Memoa"));
```

### Mixed approach

```csharp
builder.Services
    .AddMemoa(builder.Configuration.GetSection("Memoa"))
    .WriteTo.AzureBlobStorage("UseDevelopmentStorage=true");
```

## Full Configuration Schema

```json
{
  "Memoa": {
    "Enabled": true,
    "CorrelationIdHeader": "X-Correlation-Id",
    "Capture": {
      "IncludeHeaders": true,
      "HeaderAllowList": [],
      "HeaderDenyList": ["Authorization", "Cookie", "Set-Cookie", "Proxy-Authorization"],
      "IncludeQueryString": true,
      "IncludeBody": true,
      "MaxBodySizeBytes": 1048576,
      "IncludeClientIp": false,
      "IncludeRouteValues": true,
      "IncludeResponse": false
    },
    "Filters": {
      "PathIncludePatterns": ["/**"],
      "PathExcludePatterns": ["/health*", "/metrics*", "/favicon.ico"],
      "Methods": ["GET", "POST", "PUT", "PATCH", "DELETE"],
      "StatusCodeRanges": []
    },
    "Pipeline": {
      "Mode": "Background",
      "ChannelCapacity": 1024,
      "FullMode": "DropWrite",
      "WorkerCount": 1,
      "ShutdownTimeout": "00:00:30"
    },
    "Sinks": {
      "File": { ... },
      "AzureBlobStorage": { ... },
      "AmazonS3": { ... },
      "Redis": { ... }
    }
  }
}
```

## Options Reference

### Root Options (`MemoaOptions`)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | `bool` | `true` | Master switch to enable/disable capture |
| `CorrelationIdHeader` | `string` | `"X-Correlation-Id"` | Header name to extract as correlation ID |

### Capture Options (`MemoaCaptureOptions`)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IncludeHeaders` | `bool` | `true` | Capture request headers |
| `HeaderAllowList` | `string[]` | `[]` | If non-empty, only capture matching headers (glob) |
| `HeaderDenyList` | `string[]` | `[Authorization, Cookie, ...]` | Never capture these headers (glob) |
| `IncludeQueryString` | `bool` | `true` | Capture the query string |
| `IncludeBody` | `bool` | `true` | Capture the request body |
| `MaxBodySizeBytes` | `int` | `1048576` (1 MB) | Maximum body size; larger bodies are truncated |
| `IncludeClientIp` | `bool` | `false` | Capture the remote IP address |
| `IncludeRouteValues` | `bool` | `true` | Capture route values from matched endpoints |
| `IncludeResponse` | `bool` | `false` | Also capture the HTTP response |

### Filter Options (`MemoaFilterOptions`)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `PathIncludePatterns` | `string[]` | `["/**"]` | Glob patterns; request path must match at least one |
| `PathExcludePatterns` | `string[]` | `["/health*", "/metrics*", "/favicon.ico"]` | Glob patterns; matching paths are excluded |
| `Methods` | `string[]` | `[GET, POST, PUT, PATCH, DELETE]` | Only capture these HTTP methods |
| `StatusCodeRanges` | `string[]` | `[]` | Status code ranges (e.g., `"200-299"`, `"500"`); empty = all |

### Pipeline Options (`MemoaPipelineOptions`)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Mode` | `PipelineMode` | `Background` | `Background` (async, low-latency) or `Inline` (sync) |
| `ChannelCapacity` | `int` | `1024` | Bounded capacity of the background channel |
| `FullMode` | `ChannelFullMode` | `DropWrite` | Behavior when channel is full: `Wait`, `DropOldest`, `DropWrite` |
| `WorkerCount` | `int` | `1` | Number of background workers writing to sinks |
| `ShutdownTimeout` | `TimeSpan` | `30s` | Max time to drain the channel on app shutdown |

## Sink Configuration

Each sink can be configured via its own sub-section under `Memoa:Sinks`. See the
[Sinks documentation](sinks/README.md) for per-sink configuration reference.

## Environment-Specific Configuration

Use standard ASP.NET Core configuration layering:

```
appsettings.json              → base configuration
appsettings.Development.json  → dev overrides
appsettings.Production.json   → production settings
Environment variables         → MEMOA__ENABLED=false
```

Example production override that disables body capture for performance:

```json
{
  "Memoa": {
    "Capture": {
      "IncludeBody": false,
      "IncludeResponse": false
    },
    "Pipeline": {
      "ChannelCapacity": 4096,
      "WorkerCount": 2
    }
  }
}
```
