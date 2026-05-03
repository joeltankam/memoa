# Replay API

The `Memoa.Replay.Api` package injects REST API endpoints into your ASP.NET Core application,
allowing you to query captured requests and trigger replay sessions via HTTP.

## Installation

```bash
dotnet add package Memoa.Replay.Api
```

## Setup

Register the replay services and map the endpoints in your `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register Memoa capture with a sink that implements IRequestSource
builder.Services
    .AddMemoa()
    .WriteTo.AzureBlobStorage("UseDevelopmentStorage=true");

// Register replay API services
builder.Services.AddMemoaReplay(options =>
{
    options.RoutePrefix = "/replay";
    options.TargetBaseUrl = "https://localhost:5001";
    options.AuthorizationPolicy = "AdminOnly";
});

var app = builder.Build();
app.UseMemoa();

// Map replay endpoints
app.MapMemoaReplay();

app.Run();
```

## Configuration via appsettings.json

```json
{
  "Memoa": {
    "Replay": {
      "RoutePrefix": "/replay",
      "TargetBaseUrl": "https://staging.api.example.com",
      "AuthorizationPolicy": "ReplayAccess",
      "ApiKeyHeaderName": "X-Api-Key",
      "ApiKey": "my-secret-key",
      "DefaultTimelineMode": "None",
      "MaxParallelism": 10,
      "TargetAuthentication": {
        "BearerToken": "eyJhbGciOiJIUzI1NiIs..."
      }
    }
  }
}
```

```csharp
builder.Services.AddMemoaReplay(builder.Configuration.GetSection("Memoa:Replay"));
```

## Endpoints

### GET {prefix}

Query captured requests from the registered `IRequestSource`.

**Query parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `from` | `DateTimeOffset` | Only return requests captured after this time |
| `to` | `DateTimeOffset` | Only return requests captured before this time |
| `path` | `string` | Glob pattern to filter request paths |
| `methods` | `string` | Comma-separated HTTP methods |
| `take` | `int` | Maximum number of results (default: 100) |

**Response:** `200 OK` with a JSON array of `RecordedRequest` objects.

```bash
curl "https://localhost:5001/replay?from=2026-05-01T00:00:00Z&methods=POST,PUT&take=10"
```

### POST {prefix}/run

Trigger a fire-and-forget replay session. Returns immediately with `202 Accepted` and a job ID.

**Request body:**

```json
{
  "from": "2026-05-01T00:00:00Z",
  "to": "2026-05-02T00:00:00Z",
  "pathPattern": "/api/orders/**",
  "methods": ["POST", "PUT"],
  "take": 100,
  "timelineMode": "Relative",
  "parallelism": 5,
  "targetBaseUrl": "https://staging.api.example.com",
  "dryRun": false,
  "authBearerToken": "eyJhbGciOiJIUzI1NiIs..."
}
```

The `authBearerToken` field is optional. When provided, it overrides the server-configured
`TargetAuthentication` for this job only.

**Response:** `202 Accepted`

```json
{
  "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Running",
  "startedAtUtc": "2026-05-03T14:30:00+00:00"
}
```

### GET {prefix}/jobs/{id}

Poll the status of a replay job.

**Response:** `200 OK`

```json
{
  "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Completed",
  "startedAtUtc": "2026-05-03T14:30:00+00:00",
  "completedAtUtc": "2026-05-03T14:30:45+00:00",
  "result": {
    "total": 50,
    "succeeded": 48,
    "failed": 2
  }
}
```

Returns `404 Not Found` if the job ID does not exist.

## Authorization

The replay API supports two authentication mechanisms (can be combined):

### ASP.NET Core Authorization Policy

Apply a named authorization policy to all replay endpoints:

```csharp
builder.Services.AddMemoaReplay(options =>
{
    options.AuthorizationPolicy = "AdminOnly";
});
```

Requires the policy to be registered:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});
```

### API Key Authentication

Validate a static API key from a request header:

```csharp
builder.Services.AddMemoaReplay(options =>
{
    options.ApiKey = "my-secret-key";
    options.ApiKeyHeaderName = "X-Api-Key"; // default
});
```

Requests without a valid API key receive `401 Unauthorized`.

> **Note:** API key endpoint filter requires .NET 7.0 or later. On .NET 6.0, use authorization
> policies instead.

## Replay Header

All replayed requests include the `X-Memoa-Replay: true` header so the target application can
distinguish replay traffic from live traffic.

## Options Reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RoutePrefix` | `string` | `"/replay"` | Base path for all replay endpoints |
| `AuthorizationPolicy` | `string?` | `null` | ASP.NET Core policy name |
| `ApiKeyHeaderName` | `string` | `"X-Api-Key"` | Header name for API key auth |
| `ApiKey` | `string?` | `null` | Expected API key value |
| `TargetBaseUrl` | `string?` | `null` | Default replay target (null = self) |
| `DefaultTimelineMode` | `TimelineMode` | `None` | Default timeline mode |
| `MaxParallelism` | `int` | `10` | Max allowed parallelism per job |
| `TargetAuthentication` | `ReplayAuthentication?` | `null` | Default auth for the replay target (see below) |

## Target Authentication

Configure how the replay engine authenticates with the target when forwarding captured requests.
This applies to all replay jobs unless overridden by `authBearerToken` in the request body.

```csharp
builder.Services.AddMemoaReplay(options =>
{
    options.TargetBaseUrl = "https://staging.api.example.com";
    options.TargetAuthentication = new ReplayAuthentication
    {
        BearerToken = "eyJhbGciOiJIUzI1NiIs..."
    };
});
```

Alternatively, use a custom header:

```csharp
options.TargetAuthentication = new ReplayAuthentication
{
    HeaderName = "X-Api-Key",
    HeaderValue = "my-secret-key"
};
```

For advanced scenarios (OAuth token refresh, HMAC signing), use the `ConfigureRequest` callback:

```csharp
options.TargetAuthentication = new ReplayAuthentication
{
    ConfigureRequest = msg =>
    {
        var token = GetFreshOAuthToken();
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
};
```

## Prerequisites

The replay API requires an `IRequestSource` to be registered in DI. All built-in sinks implement
`IRequestSource`:

- `Memoa.Sinks.File`
- `Memoa.Sinks.AzureBlobStorage`
- `Memoa.Sinks.AmazonS3`
- `Memoa.Sinks.Redis`

If no `IRequestSource` is registered, the endpoints will throw at runtime when invoked.
