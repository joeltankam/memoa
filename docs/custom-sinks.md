# Custom Sinks

Memoa's sink architecture is designed for extensibility. Implement your own sink to
persist captured requests to any storage backend.

## Implementing IRequestSink

Create a class that implements `IRequestSink`:

```csharp
using Memoa;

public sealed class MySink : IRequestSink
{
    private readonly MyStorageClient _client;
    private readonly ILogger<MySink> _logger;

    public MySink(MyStorageClient client, ILogger<MySink> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async ValueTask WriteAsync(RecordedRequest request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(request);
        await _client.StoreAsync(request.Id.ToString(), json, cancellationToken);
        _logger.LogDebug("Wrote request {RequestId}", request.Id);
    }
}
```

## Optionally Implement IRequestSource

If your sink supports reading back captured requests (for replay scenarios):

```csharp
public sealed class MySink : IRequestSink, IRequestSource
{
    public async ValueTask WriteAsync(RecordedRequest request, CancellationToken cancellationToken)
    {
        // Write logic...
    }

    public async IAsyncEnumerable<RecordedRequest> ReadAsync(
        RequestQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Query your storage backend...
        // Apply filters from `query` (From, To, Methods, PathPattern, Take)
        // Yield matching requests
    }
}
```

## Registration Extension Method

Create an extension method on `MemoaSinkBuilder` for fluent registration:

```csharp
public static class MySinkExtensions
{
    public static MemoaSinkBuilder MySink(
        this MemoaSinkBuilder sinkBuilder,
        string connectionString,
        Action<MySinkOptions>? configure = null)
    {
        var options = new MySinkOptions { ConnectionString = connectionString };
        configure?.Invoke(options);

        sinkBuilder.Services.AddSingleton(options);
        sinkBuilder.Services.AddSingleton<IRequestSink>(sp =>
            new MySink(
                sp.GetRequiredService<MyStorageClient>(),
                sp.GetRequiredService<ILogger<MySink>>()));

        return sinkBuilder;
    }

    // IConfiguration overload for appsettings support
    public static MemoaSinkBuilder MySink(
        this MemoaSinkBuilder sinkBuilder,
        IConfiguration configuration)
    {
        var options = new MySinkOptions();
        configuration.Bind(options);

        sinkBuilder.Services.AddSingleton(options);
        sinkBuilder.Services.AddSingleton<IRequestSink>(sp =>
            new MySink(
                sp.GetRequiredService<MyStorageClient>(),
                sp.GetRequiredService<ILogger<MySink>>()));

        return sinkBuilder;
    }
}
```

## Usage

```csharp
builder.Services
    .AddMemoa()
    .WriteTo.MySink("connection-string-here");
```

## Thread Safety

Sinks must be thread-safe. When using `BackgroundRequestPipeline` with `WorkerCount > 1`,
multiple workers may call `WriteAsync` concurrently on the same sink instance.

## Error Handling

- Sinks should throw on transient failures — the pipeline logs the error and increments
  `memoa.requests.failed`
- Permanent failures (e.g., deserialization errors during read) should be logged and skipped
- Do not catch `OperationCanceledException` — let it propagate for clean shutdown

## Package Structure

A typical sink NuGet package contains:

```
MyCompany.Memoa.Sinks.MySink/
├── MySink.csproj
├── MySinkOptions.cs
├── MySink.cs
└── MySinkExtensions.cs
```

Minimal `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net6.0;net8.0;net10.0</TargetFrameworks>
    <IsPackable>true</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Memoa.Core" Version="1.0.0" />
  </ItemGroup>
</Project>
```

## RequestQuery Reference

When implementing `IRequestSource`, apply these filters:

| Property | Type | Description |
|----------|------|-------------|
| `From` | `DateTimeOffset?` | Only return requests captured at or after this time |
| `To` | `DateTimeOffset?` | Only return requests captured at or before this time |
| `PathPattern` | `string?` | Glob pattern for request paths |
| `Methods` | `IReadOnlyCollection<string>?` | Filter by HTTP methods |
| `Take` | `int?` | Maximum number of results to return |
