# Memoa Documentation

Welcome to the Memoa documentation. Memoa is an ASP.NET Core middleware library that captures
and persists HTTP requests for review, debugging, and replay.

## Table of Contents

- [Getting Started](getting-started.md) — Installation, basic setup, and first capture
- [Configuration](configuration.md) — Full reference for `MemoaOptions` and appsettings binding
- [Sinks](sinks/README.md) — Available storage backends and how to configure them
  - [File System](sinks/file.md)
  - [Azure Blob Storage](sinks/azure-blob-storage.md)
  - [Amazon S3](sinks/amazon-s3.md)
  - [Redis](sinks/redis.md)
- [Pipeline](pipeline.md) — Background vs inline modes and tuning
- [Filtering](filtering.md) — Path patterns, methods, and status code filters
- [Replay CLI](replay-cli.md) — Replaying captured requests against a target
- [Replay API](replay-api.md) — REST API endpoints for in-app replay
- [Observability](observability.md) — OpenTelemetry traces and metrics
- [Architecture](architecture.md) — Internal design and extension points
- [Custom Sinks](custom-sinks.md) — How to implement your own sink
- [Samples](samples.md) — Example applications and configurations

## Quick Links

| Package | NuGet | Description |
|---------|-------|-------------|
| `Memoa.Core` | — | Core middleware, abstractions, and pipeline |
| `Memoa.Sinks.File` | — | Local file system sink |
| `Memoa.Sinks.AzureBlobStorage` | — | Azure Blob Storage sink |
| `Memoa.Sinks.AmazonS3` | — | Amazon S3 / S3-compatible sink |
| `Memoa.Sinks.Redis` | — | Redis Streams sink |
| `Memoa.Replay.Core` | — | Shared replay engine (timeline, parallelism) |
| `Memoa.Replay.Cli` | — | .NET global tool for request replay |
| `Memoa.Replay.Api` | — | REST API endpoints for in-app replay |
