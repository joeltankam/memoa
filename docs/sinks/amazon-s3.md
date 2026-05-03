# Amazon S3 Sink

The Amazon S3 sink writes captured requests as JSON objects to an S3 bucket.
Supports AWS S3 and any S3-compatible service (MinIO, LocalStack, DigitalOcean Spaces, etc.).

## Installation

```bash
dotnet add package Memoa.Sinks.AmazonS3
```

## Registration

### Programmatic

```csharp
builder.Services
    .AddMemoa()
    .WriteTo.AmazonS3(options =>
    {
        options.BucketName = "my-memoa-bucket";
        options.Region = "us-east-1";
    });
```

### With S3-compatible endpoint (MinIO, LocalStack)

```csharp
builder.Services
    .AddMemoa()
    .WriteTo.AmazonS3(options =>
    {
        options.BucketName = "memoa";
        options.ServiceUrl = "http://localhost:9000";
        options.ForcePathStyle = true;
    });
```

### From configuration

```csharp
builder.Services
    .AddMemoa(config.GetSection("Memoa"))
    .WriteTo.AmazonS3(config.GetSection("Memoa:Sinks:AmazonS3"));
```

## Configuration Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BucketName` | `string` | `"memoa-requests"` | S3 bucket name |
| `KeyPrefix` | `string?` | `null` | Virtual directory prefix for object keys |
| `KeyFormat` | `string` | `"{year}/{month}/{day}/{hour}/{id}.json"` | Object key format with placeholders |
| `Region` | `string?` | `null` | AWS region (e.g., `"us-east-1"`) |
| `ServiceUrl` | `string?` | `null` | Custom endpoint URL for S3-compatible services |
| `ForcePathStyle` | `bool` | `false` | Force path-style addressing (required for MinIO) |
| `CreateBucketIfNotExists` | `bool` | `true` | Auto-create bucket on first write |

### appsettings.json example

```json
{
  "Memoa": {
    "Sinks": {
      "AmazonS3": {
        "BucketName": "my-memoa-bucket",
        "Region": "us-east-1",
        "KeyPrefix": "production",
        "KeyFormat": "{year}/{month}/{day}/{hour}/{id}.json",
        "CreateBucketIfNotExists": true
      }
    }
  }
}
```

### S3-compatible service (MinIO)

```json
{
  "Memoa": {
    "Sinks": {
      "AmazonS3": {
        "BucketName": "memoa",
        "ServiceUrl": "http://localhost:9000",
        "ForcePathStyle": true
      }
    }
  }
}
```

## Key Name Placeholders

| Placeholder | Value |
|-------------|-------|
| `{year}` | 4-digit year |
| `{month}` | 2-digit month |
| `{day}` | 2-digit day |
| `{hour}` | 2-digit hour (UTC) |
| `{id}` | Request unique GUID |
| `{method}` | HTTP method |

## Authentication

The sink uses the default AWS credential chain:

1. Environment variables (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`)
2. AWS credentials file (`~/.aws/credentials`)
3. IAM role (EC2 instance profile, ECS task role, EKS IRSA)

If a pre-registered `IAmazonS3` service exists in DI, it will be used instead of creating a new client.

## Local Development

Use [LocalStack](https://localstack.cloud/) or [MinIO](https://min.io/):

```bash
# LocalStack
docker run -p 4566:4566 localstack/localstack

# MinIO
docker run -p 9000:9000 minio/minio server /data
```

## Performance Considerations

- Objects are uploaded with `ContentType: application/json`
- Bucket existence is verified once (on first write) and cached
- The `PutBucketAsync` call is idempotent — "BucketAlreadyOwnedByYou" errors are handled gracefully
- Listing uses pagination with continuation tokens for large datasets
