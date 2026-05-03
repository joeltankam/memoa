# Filtering

Memoa provides multiple filtering mechanisms to control which HTTP requests are captured.
Filters are evaluated in the middleware before any capture processing occurs.

## Filter Types

### Path Include Patterns

Glob patterns that a request path must match (at least one) to be captured:

```json
{
  "Memoa": {
    "Filters": {
      "PathIncludePatterns": ["/api/**", "/webhooks/**"]
    }
  }
}
```

Default: `["/**"]` (all paths).

### Path Exclude Patterns

Glob patterns that cause matching requests to be skipped, even if they match include patterns:

```json
{
  "Memoa": {
    "Filters": {
      "PathExcludePatterns": ["/health*", "/metrics*", "/favicon.ico", "/_*"]
    }
  }
}
```

Default: `["/health*", "/metrics*", "/favicon.ico"]`.

### HTTP Methods

Only requests with these HTTP methods are captured:

```json
{
  "Memoa": {
    "Filters": {
      "Methods": ["GET", "POST", "PUT", "PATCH", "DELETE"]
    }
  }
}
```

Default: `["GET", "POST", "PUT", "PATCH", "DELETE"]`.

### Status Code Ranges

When response capture is enabled, filter by status code:

```json
{
  "Memoa": {
    "Filters": {
      "StatusCodeRanges": ["400-499", "500-599"]
    }
  }
}
```

Default: `[]` (all status codes).

Format: single codes (`"404"`) or ranges (`"400-499"`).

## Glob Pattern Syntax

| Pattern | Matches |
|---------|---------|
| `*` | Any sequence of characters except `/` |
| `**` | Any sequence including `/` (recursive) |
| `?` | Any single character |

### Examples

| Pattern | Matches | Does Not Match |
|---------|---------|----------------|
| `/api/**` | `/api/users`, `/api/v2/orders/123` | `/webhooks/stripe` |
| `/api/*/items` | `/api/orders/items` | `/api/orders/123/items` |
| `/health*` | `/health`, `/healthz`, `/health/live` | `/api/health` |
| `/**/*.json` | `/data/file.json`, `/a/b/c.json` | `/data/file.xml` |

## Filter Evaluation Order

```
1. Is Memoa enabled?           → No  → Skip
2. Does method match?          → No  → Skip
3. Does path match include?    → No  → Skip
4. Does path match exclude?    → Yes → Skip
5. (After response) Status OK? → No  → Skip
6. ✓ Capture the request
```

## Header Filtering

Header filtering controls which headers are included in the capture (not whether the request
is captured):

```json
{
  "Memoa": {
    "Capture": {
      "HeaderAllowList": ["X-*", "Content-Type", "Accept"],
      "HeaderDenyList": ["Authorization", "Cookie", "Set-Cookie", "Proxy-Authorization"]
    }
  }
}
```

**Evaluation:**

1. If `HeaderAllowList` is non-empty, only headers matching at least one allow pattern are included
2. Headers matching any `HeaderDenyList` pattern are always excluded
3. The deny list takes precedence over the allow list

## Programmatic Configuration

```csharp
builder.Services.AddMemoa(options =>
{
    options.Filters.PathIncludePatterns = ["/api/**"];
    options.Filters.PathExcludePatterns = ["/api/health"];
    options.Filters.Methods = ["POST", "PUT", "DELETE"];
    options.Capture.HeaderDenyList = ["Authorization", "Cookie", "X-Internal-*"];
});
```

## Metrics

Filtered (skipped) requests are tracked via the `memoa.requests.skipped` counter.
See [Observability](observability.md).
