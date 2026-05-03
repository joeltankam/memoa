# Getting Started

## Installation

Install the core package and at least one sink:

```bash
# Core middleware (required)
dotnet add package Memoa.Core

# Choose one or more sinks:
dotnet add package Memoa.Sinks.File
dotnet add package Memoa.Sinks.AzureBlobStorage
dotnet add package Memoa.Sinks.AmazonS3
dotnet add package Memoa.Sinks.Redis
```

## Minimum Setup

Add Memoa to your ASP.NET Core application in `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register Memoa with a file system sink
builder.Services
    .AddMemoa()
    .WriteTo.FileSystem("./captured-requests");

var app = builder.Build();

// Add the middleware early in the pipeline
app.UseMemoa();

app.MapGet("/", () => "Hello World!");
app.Run();
```

Every HTTP request matching the default filters will now be captured and saved as a JSON file.

## Configuration via appsettings.json

Instead of programmatic configuration, bind from your configuration:

```csharp
builder.Services
    .AddMemoa(builder.Configuration.GetSection("Memoa"))
    .WriteTo.FileSystem(builder.Configuration.GetSection("Memoa:Sinks:File"));
```

```json
{
  "Memoa": {
    "Enabled": true,
    "Capture": {
      "IncludeHeaders": true,
      "IncludeBody": true,
      "MaxBodySizeBytes": 1048576
    },
    "Filters": {
      "PathIncludePatterns": ["/**"],
      "PathExcludePatterns": ["/health*", "/metrics*"]
    },
    "Sinks": {
      "File": {
        "OutputDirectory": "./captured-requests",
        "IndentJson": true
      }
    }
  }
}
```

## What Gets Captured

By default, Memoa captures:

- HTTP method, scheme, host, path, query string, and protocol version
- Request headers (excluding sensitive headers like `Authorization`, `Cookie`)
- Request body (up to 1 MB)
- A unique request ID and UTC timestamp
- Correlation ID from the `X-Correlation-Id` header (if present)

## Captured Request Format

Each request is stored as a JSON document:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "capturedAtUtc": "2026-05-03T14:30:00+00:00",
  "correlationId": "abc-123",
  "method": "POST",
  "scheme": "https",
  "host": "api.example.com",
  "path": "/api/orders",
  "queryString": "?status=pending",
  "protocol": "HTTP/2",
  "headers": {
    "Content-Type": ["application/json"],
    "Accept": ["application/json"]
  },
  "body": {
    "contentType": "application/json",
    "length": 42,
    "text": "{\"productId\": 1, \"quantity\": 2}"
  }
}
```

## Next Steps

- [Configuration Reference](configuration.md) — Full options documentation
- [Sinks](sinks/README.md) — Choose and configure a storage backend
- [Filtering](filtering.md) — Control which requests are captured
- [Pipeline](pipeline.md) — Tune performance with background vs inline modes
