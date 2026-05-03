# Replay CLI

The Memoa Replay CLI is a .NET global tool that reads captured requests from any configured
source and replays them against a target HTTP endpoint, optionally reproducing the original
request timeline.

## Installation

```bash
dotnet tool install --global Memoa.Replay.Cli
```

## Basic Usage

```bash
memoa-replay \
  --source azure \
  --connection-string "UseDevelopmentStorage=true" \
  --target https://localhost:5001
```

## Options

### Required

| Option | Alias | Description |
|--------|-------|-------------|
| `--source` | `-s` | Source backend: `azure`, `file`, `s3`, `redis` |
| `--target` | `-t` | Base URL to replay requests against |

### Timeline and Pacing

| Option | Default | Description |
|--------|---------|-------------|
| `--timeline` | `none` | Timeline mode: `none` (parallel/delay) or `relative` (preserve original timing) |
| `--parallelism` | `1` | Concurrent requests (only when `--timeline none`) |
| `--delay` | `0` | Fixed delay between requests in ms (only when `--timeline none`) |
| `--dry-run` | `false` | Print requests without sending |

### Authentication

| Option | Description |
|--------|-------------|
| `--auth-token` | Bearer token sent as `Authorization: Bearer {token}` to the target |
| `--auth-header` | Custom auth header in `Name:Value` format (e.g., `X-Api-Key:secret`) |

`--auth-token` and `--auth-header` are mutually exclusive.

### Query Filters

| Option | Description |
|--------|-------------|
| `--from` | Only replay requests captured after this UTC time |
| `--to` | Only replay requests captured before this UTC time |
| `--methods` | Only replay these HTTP methods |
| `--path` | Glob pattern to filter request paths |

### Source: Azure Blob Storage (`--source azure`)

| Option | Alias | Default | Description |
|--------|-------|---------|-------------|
| `--connection-string` | `-c` | — | Azure Storage connection string (required) |
| `--container` | — | `memoa-requests` | Blob container name |
| `--prefix` | — | — | Blob prefix filter |

### Source: File System (`--source file`)

| Option | Alias | Description |
|--------|-------|-------------|
| `--directory` | `-d` | Path to the captured requests directory (required) |

### Source: Amazon S3 (`--source s3`)

| Option | Description |
|--------|-------------|
| `--bucket` | S3 bucket name (required) |
| `--region` | AWS region |
| `--service-url` | S3-compatible service URL (e.g., MinIO, LocalStack) |

### Source: Redis (`--source redis`)

| Option | Default | Description |
|--------|---------|-------------|
| `--redis-connection` | — | Redis connection string (required) |
| `--stream-key` | `memoa:requests` | Redis stream key |

## Timeline Replay

The `--timeline` option controls inter-request timing:

- **`none`** (default): Fire requests as fast as possible, limited by `--parallelism` and `--delay`.
- **`relative`**: Reproduce the exact timing between original requests. The tool computes the delta
  between consecutive `CapturedAtUtc` timestamps and waits that duration before sending the next
  request. This reproduces the exact load pattern the API originally received.

When `--timeline relative` is set, `--parallelism` is ignored (requests are always sequential).

## Replay Header

All replayed requests include the header `X-Memoa-Replay: true` so the target application can
distinguish replay traffic from live traffic.

## Examples

### Replay from file system with timeline reproduction

```bash
memoa-replay \
  --source file \
  --directory ./captured-requests \
  --target https://staging.api.example.com \
  --timeline relative
```

### Replay from S3 with filters

```bash
memoa-replay \
  --source s3 \
  --bucket my-memoa-bucket \
  --region us-east-1 \
  --target https://localhost:5001 \
  --from "2026-05-01T00:00:00Z" \
  --methods POST PUT
```

### Replay from Redis

```bash
memoa-replay \
  --source redis \
  --redis-connection "localhost:6379" \
  --stream-key "memoa:requests" \
  --target https://localhost:5001
```

### Parallel load replay from Azure

```bash
memoa-replay \
  --source azure \
  -c "UseDevelopmentStorage=true" \
  -t https://load-test.example.com \
  --parallelism 10 \
  --delay 100
```

### Replay with bearer token authentication

```bash
memoa-replay \
  --source azure \
  -c "UseDevelopmentStorage=true" \
  -t https://staging.api.example.com \
  --auth-token "eyJhbGciOiJIUzI1NiIs..."
```

### Replay with custom API key header

```bash
memoa-replay \
  --source file \
  -d ./captured-requests \
  -t https://staging.api.example.com \
  --auth-header "X-Api-Key:my-secret-key"
```

### Dry run to preview

```bash
memoa-replay \
  --source file \
  -d ./captured-requests \
  -t https://localhost:5001 \
  --dry-run
```

Output:

```text
[DRY-RUN] POST /api/orders?status=new
[DRY-RUN] PUT /api/orders/123
[DRY-RUN] GET /api/products
```

## Replay Behavior

- **Headers**: Original request headers are forwarded (except `Host`, `Content-*`, `Transfer-Encoding`)
- **Replay header**: `X-Memoa-Replay: true` is added to every request
- **Body**: Request body is replayed exactly as captured (text or binary)
- **Content-Type**: Preserved from the original request
- **Method**: Original HTTP method is used
- **Path + Query**: Appended to the `--target` base URL

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | All requests replayed successfully |
| `1` | One or more requests failed |

## Output

```text
[OK] POST /api/orders → 201 (3fa85f64-5717-4562-b3fc-2c963f66afa6)
[OK] GET /api/products → 200 (7c9e6679-7425-40de-944b-e07fc1f90ae7)
[FAIL] PUT /api/orders/999 (a1b2c3d4-...): Connection refused

Replay complete: 3 total, 2 succeeded, 1 failed
```

