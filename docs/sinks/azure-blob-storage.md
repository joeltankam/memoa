# Azure Blob Storage Sink

The Azure Blob Storage sink writes captured requests as JSON blobs to an Azure Storage container.
Ideal for production workloads hosted on Azure with durable, scalable storage needs.

## Installation

```bash
dotnet add package Memoa.Sinks.AzureBlobStorage
```

## Registration

### With connection string

```csharp
builder.Services
    .AddMemoa()
    .WriteTo.AzureBlobStorage("DefaultEndpointsProtocol=https;AccountName=...");
```

### With options

```csharp
builder.Services
    .AddMemoa()
    .WriteTo.AzureBlobStorage("UseDevelopmentStorage=true", options =>
    {
        options.ContainerName = "my-requests";
        options.BlobPrefix = "api-v2";
        options.CreateContainerIfNotExists = true;
    });
```

### With pre-registered BlobServiceClient (Azure.Identity)

```csharp
builder.Services.AddAzureClients(azure =>
{
    azure.AddBlobServiceClient(new Uri("https://myaccount.blob.core.windows.net"));
    azure.UseCredential(new DefaultAzureCredential());
});

builder.Services
    .AddMemoa()
    .WriteTo.AzureBlobStorage(options =>
    {
        options.ContainerName = "memoa-requests";
    });
```

### From configuration

```csharp
builder.Services
    .AddMemoa(config.GetSection("Memoa"))
    .WriteTo.AzureBlobStorage(config.GetSection("Memoa:Sinks:AzureBlobStorage"));
```

## Configuration Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ConnectionString` | `string?` | `null` | Azure Storage connection string |
| `ServiceUri` | `Uri?` | `null` | Blob service endpoint (for `DefaultAzureCredential`) |
| `ContainerName` | `string` | `"memoa-requests"` | Blob container name |
| `BlobPrefix` | `string?` | `null` | Virtual directory prefix for blob names |
| `CreateContainerIfNotExists` | `bool` | `true` | Auto-create the container on first write |
| `BlobNameFormat` | `string` | `"{year}/{month}/{day}/{hour}/{id}.json"` | Blob name format with placeholders |

### appsettings.json example

```json
{
  "Memoa": {
    "Sinks": {
      "AzureBlobStorage": {
        "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;...",
        "ContainerName": "memoa-requests",
        "BlobPrefix": "production",
        "CreateContainerIfNotExists": true,
        "BlobNameFormat": "{year}/{month}/{day}/{hour}/{id}.json"
      }
    }
  }
}
```

## Blob Name Placeholders

| Placeholder | Value |
|-------------|-------|
| `{year}` | 4-digit year |
| `{month}` | 2-digit month |
| `{day}` | 2-digit day |
| `{hour}` | 2-digit hour (UTC) |
| `{id}` | Request unique GUID |
| `{method}` | HTTP method |

## Authentication

The sink supports multiple authentication modes:

1. **Connection string** — Simplest, includes account key
2. **DefaultAzureCredential** — Register a `BlobServiceClient` with `Azure.Identity`
3. **Managed Identity** — Works automatically in Azure App Service / AKS

## Local Development

Use the [Azurite emulator](https://github.com/Azure/Azurite) for local development:

```bash
docker run -p 10000:10000 mcr.microsoft.com/azure-storage/azurite
```

Connection string: `UseDevelopmentStorage=true`

## Performance Considerations

- Blobs are uploaded with `ContentType: application/json`
- Container existence is verified once (on first write) and cached
- Use the Background pipeline mode for minimal request latency impact
- Consider blob lifecycle policies for automatic cleanup of old captures
