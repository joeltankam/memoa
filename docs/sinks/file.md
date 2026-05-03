# File System Sink

The file system sink writes captured requests as individual JSON files to a local directory.
Ideal for development, debugging, and scenarios where external dependencies are undesirable.

## Installation

```bash
dotnet add package Memoa.Sinks.File
```

## Registration

### Programmatic

```csharp
builder.Services
    .AddMemoa()
    .WriteTo.FileSystem("./captured-requests");
```

With additional options:

```csharp
builder.Services
    .AddMemoa()
    .WriteTo.FileSystem("./captured-requests", options =>
    {
        options.FileNameFormat = "{year}/{month}/{day}/{method}_{id}.json";
        options.IndentJson = false;
    });
```

### From configuration

```csharp
builder.Services
    .AddMemoa(config.GetSection("Memoa"))
    .WriteTo.FileSystem(config.GetSection("Memoa:Sinks:File"));
```

## Configuration Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `OutputDirectory` | `string` | `"./memoa-requests"` | Root directory for captured request files |
| `FileNameFormat` | `string` | `"{year}/{month}/{day}/{hour}/{id}.json"` | Path format with placeholders |
| `IndentJson` | `bool` | `true` | Whether to pretty-print JSON output |

### appsettings.json example

```json
{
  "Memoa": {
    "Sinks": {
      "File": {
        "OutputDirectory": "./captured-requests",
        "FileNameFormat": "{year}/{month}/{day}/{hour}/{id}.json",
        "IndentJson": true
      }
    }
  }
}
```

## File Name Placeholders

| Placeholder | Value |
|-------------|-------|
| `{year}` | 4-digit year (e.g., `2026`) |
| `{month}` | 2-digit month (e.g., `05`) |
| `{day}` | 2-digit day (e.g., `03`) |
| `{hour}` | 2-digit hour in UTC (e.g., `14`) |
| `{id}` | The request's unique GUID |
| `{method}` | HTTP method (e.g., `GET`, `POST`) |

## Directory Structure

With the default format, files are organized as:

```
captured-requests/
├── 2026/
│   └── 05/
│       └── 03/
│           ├── 14/
│           │   ├── 3fa85f64-5717-4562-b3fc-2c963f66afa6.json
│           │   └── 7c9e6679-7425-40de-944b-e07fc1f90ae7.json
│           └── 15/
│               └── ...
```

## Reading Back

The file sink also implements `IRequestSource`, allowing you to read captured requests:

```csharp
var sink = serviceProvider.GetRequiredService<FileSink>();
await foreach (var request in sink.ReadAsync(new RequestQuery { From = yesterday }, ct))
{
    // Process request
}
```

## Performance Notes

- File I/O is async on .NET 8+ (uses `File.WriteAllBytesAsync`)
- On .NET 6, falls back to `FileStream` with async flag
- Directories are created lazily on first write
- For high-throughput scenarios, consider the Background pipeline mode
