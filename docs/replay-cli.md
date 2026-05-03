# Replay CLI

The Memoa Replay CLI is a .NET global tool that reads captured requests from a source
and replays them against a target HTTP endpoint.

## Installation

```bash
dotnet tool install --global Memoa.Replay.Cli
```

## Basic Usage

```bash
memoa-replay \
  --connection-string "UseDevelopmentStorage=true" \
  --target https://localhost:5001
```

## Options

| Option | Alias | Required | Default | Description |
|--------|-------|----------|---------|-------------|
| `--connection-string` | `-c` | Yes | — | Azure Storage connection string for the source |
| `--target` | `-t` | Yes | — | Base URL to replay requests against |
| `--container` | — | No | `memoa-requests` | Blob container name |
| `--prefix` | — | No | — | Blob prefix filter |
| `--from` | — | No | — | Only replay requests after this UTC time |
| `--to` | — | No | — | Only replay requests before this UTC time |
| `--methods` | — | No | — | Only replay these HTTP methods |
| `--path` | — | No | — | Glob pattern to filter request paths |
| `--dry-run` | — | No | `false` | Print requests without sending |
| `--parallelism` | — | No | `1` | Concurrent replay requests |
| `--delay` | — | No | `0` | Delay between requests (ms) |

## Examples

### Replay POST requests from a specific time range

```bash
memoa-replay \
  -c "DefaultEndpointsProtocol=https;AccountName=..." \
  -t https://staging.api.example.com \
  --from "2026-05-01T00:00:00Z" \
  --to "2026-05-02T00:00:00Z" \
  --methods POST PUT
```

### Dry run to preview what would be replayed

```bash
memoa-replay \
  -c "UseDevelopmentStorage=true" \
  -t https://localhost:5001 \
  --dry-run
```

Output:

```
[DRY-RUN] POST /api/orders?status=new
[DRY-RUN] PUT /api/orders/123
[DRY-RUN] GET /api/products
```

### Parallel replay with delay

```bash
memoa-replay \
  -c "UseDevelopmentStorage=true" \
  -t https://load-test.example.com \
  --parallelism 10 \
  --delay 100
```

### Filter by path pattern

```bash
memoa-replay \
  -c "UseDevelopmentStorage=true" \
  -t https://localhost:5001 \
  --path "/api/orders/**"
```

## Replay Behavior

- **Headers**: Original request headers are forwarded (except `Host`, `Content-*`, `Transfer-Encoding`)
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

```
[OK] POST /api/orders (3fa85f64-5717-4562-b3fc-2c963f66afa6)
[OK] GET /api/products (7c9e6679-7425-40de-944b-e07fc1f90ae7)
[FAIL] PUT /api/orders/999 (a1b2c3d4-...): 404 Not Found

Replay complete: 3 total, 2 succeeded, 1 failed
```

## Current Limitations

- Currently reads from Azure Blob Storage only (additional source support planned)
- No request transformation/modification before replay
- No response validation (status codes are not checked against original)
