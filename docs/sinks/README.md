# Sinks

Sinks are storage backends that persist captured HTTP requests. Memoa supports multiple sinks
simultaneously — every captured request is written to all registered sinks.

## Available Sinks

| Sink | Package | Best For |
|------|---------|----------|
| [File System](file.md) | `Memoa.Sinks.File` | Local development, debugging |
| [Azure Blob Storage](azure-blob-storage.md) | `Memoa.Sinks.AzureBlobStorage` | Azure-hosted production workloads |
| [Amazon S3](amazon-s3.md) | `Memoa.Sinks.AmazonS3` | AWS-hosted workloads, S3-compatible stores |
| [Redis](redis.md) | `Memoa.Sinks.Redis` | Real-time streaming, short-lived retention |

## Registration Pattern

All sinks follow the same registration pattern using the fluent `WriteTo` builder:

```csharp
builder.Services
    .AddMemoa()
    .WriteTo.FileSystem("./requests")
    .WriteTo.Redis("localhost:6379");
```

## Configuration via appsettings.json

Each sink also supports binding from an `IConfiguration` section:

```csharp
var memoaSection = builder.Configuration.GetSection("Memoa");

builder.Services
    .AddMemoa(memoaSection)
    .WriteTo.FileSystem(memoaSection.GetSection("Sinks:File"))
    .WriteTo.AzureBlobStorage(memoaSection.GetSection("Sinks:AzureBlobStorage"));
```

Example `appsettings.json`:

```json
{
  "Memoa": {
    "Sinks": {
      "File": {
        "OutputDirectory": "./captured-requests",
        "FileNameFormat": "{year}/{month}/{day}/{hour}/{id}.json",
        "IndentJson": true
      },
      "AzureBlobStorage": {
        "ConnectionString": "UseDevelopmentStorage=true",
        "ContainerName": "memoa-requests"
      },
      "AmazonS3": {
        "BucketName": "my-memoa-bucket",
        "Region": "us-east-1"
      },
      "Redis": {
        "ConnectionString": "localhost:6379",
        "StreamKey": "memoa:requests",
        "MaxLength": 10000
      }
    }
  }
}
```

## Multiple Sinks

You can register multiple sinks. All captured requests are written to every registered sink:

```csharp
builder.Services
    .AddMemoa()
    .WriteTo.FileSystem("./local-backup")
    .WriteTo.AzureBlobStorage("DefaultEndpointsProtocol=...")
    .WriteTo.Redis("localhost:6379");
```

## Read-Back (IRequestSource)

All sinks also implement `IRequestSource`, allowing you to read captured requests back
for replay or analysis:

```csharp
var source = serviceProvider.GetRequiredService<IRequestSource>();
await foreach (var request in source.ReadAsync(new RequestQuery { From = yesterday }, ct))
{
    Console.WriteLine($"{request.Method} {request.Path}");
}
```

## Custom Sinks

See [Custom Sinks](../custom-sinks.md) for instructions on implementing your own sink.
