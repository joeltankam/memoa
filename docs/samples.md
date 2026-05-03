# Samples

## Minimal API with File Sink

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddMemoa()
    .WriteTo.FileSystem("./captured");

var app = builder.Build();
app.UseMemoa();

app.MapGet("/hello", () => "Hello, World!");
app.MapPost("/echo", (HttpRequest request) => Results.Stream(request.Body));

app.Run();
```

## Configuration-Driven Setup

`Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);
var memoaSection = builder.Configuration.GetSection("Memoa");

builder.Services
    .AddMemoa(memoaSection)
    .WriteTo.AzureBlobStorage(memoaSection.GetSection("Sinks:AzureBlobStorage"))
    .WriteTo.Redis(memoaSection.GetSection("Sinks:Redis"));

var app = builder.Build();
app.UseMemoa();
app.MapControllers();
app.Run();
```

`appsettings.json`:

```json
{
  "Memoa": {
    "Enabled": true,
    "Capture": {
      "IncludeHeaders": true,
      "IncludeBody": true,
      "IncludeResponse": true,
      "MaxBodySizeBytes": 524288
    },
    "Filters": {
      "PathIncludePatterns": ["/api/**"],
      "PathExcludePatterns": ["/api/health"],
      "Methods": ["POST", "PUT", "PATCH", "DELETE"]
    },
    "Pipeline": {
      "Mode": "Background",
      "ChannelCapacity": 2048,
      "WorkerCount": 2
    },
    "Sinks": {
      "AzureBlobStorage": {
        "ConnectionString": "UseDevelopmentStorage=true",
        "ContainerName": "api-requests",
        "BlobPrefix": "v2"
      },
      "Redis": {
        "ConnectionString": "localhost:6379",
        "StreamKey": "api:captured",
        "MaxLength": 5000
      }
    }
  }
}
```

## Multi-Sink with OpenTelemetry

```csharp
var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("Memoa").AddOtlpExporter())
    .WithMetrics(m => m.AddMeter("Memoa").AddOtlpExporter());

// Memoa with multiple sinks
builder.Services
    .AddMemoa(options =>
    {
        options.Capture.IncludeResponse = true;
        options.Pipeline.WorkerCount = 2;
    })
    .WriteTo.FileSystem("./debug-captures", o => o.IndentJson = true)
    .WriteTo.AmazonS3(o =>
    {
        o.BucketName = "prod-api-captures";
        o.Region = "eu-west-1";
        o.KeyPrefix = "service-a";
    });

var app = builder.Build();
app.UseMemoa();
app.MapControllers();
app.Run();
```

## Selective Capture (Audit Trail)

Capture only mutation operations for compliance:

```csharp
builder.Services.AddMemoa(options =>
{
    options.Capture.IncludeHeaders = true;
    options.Capture.IncludeBody = true;
    options.Capture.IncludeResponse = true;
    options.Capture.IncludeClientIp = true;
    options.Filters.Methods = ["POST", "PUT", "PATCH", "DELETE"];
    options.Filters.PathIncludePatterns = ["/api/admin/**", "/api/users/**"];
    options.Pipeline.Mode = PipelineMode.Inline;  // Guarantee capture
});
```

## Development vs Production Configuration

`appsettings.Development.json`:

```json
{
  "Memoa": {
    "Capture": { "IncludeBody": true, "IncludeResponse": true },
    "Pipeline": { "Mode": "Inline" },
    "Sinks": {
      "File": { "OutputDirectory": "./dev-captures", "IndentJson": true }
    }
  }
}
```

`appsettings.Production.json`:

```json
{
  "Memoa": {
    "Capture": { "IncludeBody": false },
    "Pipeline": { "Mode": "Background", "ChannelCapacity": 4096, "WorkerCount": 4 },
    "Sinks": {
      "AzureBlobStorage": {
        "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
        "ContainerName": "prod-captures"
      }
    }
  }
}
```

## Replay Workflow

1. **Capture** requests in production:

```csharp
builder.Services.AddMemoa(config.GetSection("Memoa"))
    .WriteTo.AzureBlobStorage(config.GetSection("Memoa:Sinks:AzureBlobStorage"));
```

2. **Replay** against staging:

```bash
memoa-replay \
  -c "DefaultEndpointsProtocol=https;AccountName=..." \
  -t https://staging.api.example.com \
  --from "2026-05-01" \
  --methods POST PUT DELETE \
  --parallelism 5
```
