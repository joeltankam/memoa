using System.CommandLine;
using System.CommandLine.Invocation;
using Amazon;
using Amazon.S3;
using Azure.Storage.Blobs;
using Memoa;
using Memoa.Replay;
using Memoa.Sinks.AmazonS3;
using Memoa.Sinks.AzureBlobStorage;
using Memoa.Sinks.File;
using Memoa.Sinks.Redis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace Memoa.Replay.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Source selection
        var sourceOption = new Option<string>("--source", ["-s"]) { Description = "Source backend: azure, file, s3, redis.", Required = true };
        var targetOption = new Option<string>("--target", ["-t"]) { Description = "Base URL to replay requests against.", Required = true };

        // Timeline & pacing
        var timelineOption = new Option<string>("--timeline") { Description = "Timeline mode: none, relative.", DefaultValueFactory = _ => "none" };
        var parallelismOption = new Option<int>("--parallelism") { Description = "Number of concurrent requests (timeline=none only).", DefaultValueFactory = _ => 1 };
        var delayOption = new Option<int>("--delay") { Description = "Delay between requests in milliseconds (timeline=none only).", DefaultValueFactory = _ => 0 };
        var dryRunOption = new Option<bool>("--dry-run") { Description = "Print requests without sending them." };

        // Query filters
        var fromOption = new Option<DateTimeOffset?>("--from") { Description = "Only replay requests captured after this UTC time." };
        var toOption = new Option<DateTimeOffset?>("--to") { Description = "Only replay requests captured before this UTC time." };
        var methodsOption = new Option<string[]?>("--methods") { Description = "Only replay these HTTP methods." };
        var pathPatternOption = new Option<string?>("--path") { Description = "Glob pattern to filter request paths." };

        // Azure Blob Storage options
        var connectionStringOption = new Option<string?>("--connection-string", ["-c"]) { Description = "Azure Storage connection string (source=azure)." };
        var containerOption = new Option<string>("--container") { Description = "Blob container name (source=azure).", DefaultValueFactory = _ => "memoa-requests" };
        var prefixOption = new Option<string?>("--prefix") { Description = "Blob prefix (source=azure)." };

        // File options
        var directoryOption = new Option<string?>("--directory", ["-d"]) { Description = "Directory path (source=file)." };

        // Amazon S3 options
        var bucketOption = new Option<string?>("--bucket") { Description = "S3 bucket name (source=s3)." };
        var regionOption = new Option<string?>("--region") { Description = "AWS region (source=s3)." };
        var serviceUrlOption = new Option<string?>("--service-url") { Description = "S3-compatible service URL (source=s3)." };

        // Redis options
        var redisConnectionOption = new Option<string?>("--redis-connection") { Description = "Redis connection string (source=redis)." };
        var streamKeyOption = new Option<string>("--stream-key") { Description = "Redis stream key (source=redis).", DefaultValueFactory = _ => "memoa:requests" };

        var rootCommand = new RootCommand("Replay HTTP requests captured by Memoa middleware.")
        {
            sourceOption,
            targetOption,
            timelineOption,
            parallelismOption,
            delayOption,
            dryRunOption,
            fromOption,
            toOption,
            methodsOption,
            pathPatternOption,
            connectionStringOption,
            containerOption,
            prefixOption,
            directoryOption,
            bucketOption,
            regionOption,
            serviceUrlOption,
            redisConnectionOption,
            streamKeyOption
        };

        rootCommand.Action = new ReplayAction(
            sourceOption, targetOption, timelineOption, parallelismOption, delayOption, dryRunOption,
            fromOption, toOption, methodsOption, pathPatternOption,
            connectionStringOption, containerOption, prefixOption,
            directoryOption,
            bucketOption, regionOption, serviceUrlOption,
            redisConnectionOption, streamKeyOption);

        var config = new CommandLineConfiguration(rootCommand);
        return await config.InvokeAsync(args).ConfigureAwait(false);
    }

    private sealed class ReplayAction : AsynchronousCommandLineAction
    {
        private readonly Option<string> _source;
        private readonly Option<string> _target;
        private readonly Option<string> _timeline;
        private readonly Option<int> _parallelism;
        private readonly Option<int> _delay;
        private readonly Option<bool> _dryRun;
        private readonly Option<DateTimeOffset?> _from;
        private readonly Option<DateTimeOffset?> _to;
        private readonly Option<string[]?> _methods;
        private readonly Option<string?> _pathPattern;
        private readonly Option<string?> _connectionString;
        private readonly Option<string> _container;
        private readonly Option<string?> _prefix;
        private readonly Option<string?> _directory;
        private readonly Option<string?> _bucket;
        private readonly Option<string?> _region;
        private readonly Option<string?> _serviceUrl;
        private readonly Option<string?> _redisConnection;
        private readonly Option<string> _streamKey;

        public ReplayAction(
            Option<string> source, Option<string> target, Option<string> timeline,
            Option<int> parallelism, Option<int> delay, Option<bool> dryRun,
            Option<DateTimeOffset?> from, Option<DateTimeOffset?> to,
            Option<string[]?> methods, Option<string?> pathPattern,
            Option<string?> connectionString, Option<string> container, Option<string?> prefix,
            Option<string?> directory,
            Option<string?> bucket, Option<string?> region, Option<string?> serviceUrl,
            Option<string?> redisConnection, Option<string> streamKey)
        {
            _source = source;
            _target = target;
            _timeline = timeline;
            _parallelism = parallelism;
            _delay = delay;
            _dryRun = dryRun;
            _from = from;
            _to = to;
            _methods = methods;
            _pathPattern = pathPattern;
            _connectionString = connectionString;
            _container = container;
            _prefix = prefix;
            _directory = directory;
            _bucket = bucket;
            _region = region;
            _serviceUrl = serviceUrl;
            _redisConnection = redisConnection;
            _streamKey = streamKey;
        }

        public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
        {
            var source = parseResult.GetValue(_source)!;
            var target = parseResult.GetValue(_target)!;
            var timelineStr = parseResult.GetValue(_timeline)!;
            var parallelism = parseResult.GetValue(_parallelism);
            var delay = parseResult.GetValue(_delay);
            var dryRun = parseResult.GetValue(_dryRun);

            var timelineMode = timelineStr.Equals("relative", StringComparison.OrdinalIgnoreCase)
                ? TimelineMode.Relative
                : TimelineMode.None;

            var query = new RequestQuery
            {
                From = parseResult.GetValue(_from),
                To = parseResult.GetValue(_to),
                Methods = parseResult.GetValue(_methods),
                PathPattern = parseResult.GetValue(_pathPattern)
            };

            IRequestSource requestSource;
            try
            {
                requestSource = CreateSource(source, parseResult);
            }
            catch (InvalidOperationException ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}").ConfigureAwait(false);
                return 1;
            }

            var replayOptions = new ReplayOptions
            {
                Mode = timelineMode,
                Parallelism = parallelism,
                DelayMs = delay,
                DryRun = dryRun,
                TargetBaseUrl = target
            };

            using var httpClient = new HttpClient { BaseAddress = new Uri(target) };
            var replayer = new RequestReplayer(httpClient, replayOptions, NullLogger<RequestReplayer>.Instance);

            var result = await replayer.ReplayAsync(
                requestSource.ReadAsync(query, cancellationToken),
                outcome =>
                {
                    if (dryRun)
                    {
                        Console.Out.WriteLine($"[DRY-RUN] {outcome.Request.Method} {outcome.Request.Path}{outcome.Request.QueryString ?? ""}");
                    }
                    else if (outcome.Success)
                    {
                        Console.Out.WriteLine($"[OK] {outcome.Request.Method} {outcome.Request.Path} → {outcome.StatusCode} ({outcome.Request.Id})");
                    }
                    else
                    {
                        Console.Error.WriteLine($"[FAIL] {outcome.Request.Method} {outcome.Request.Path} ({outcome.Request.Id}): {outcome.Error}");
                    }
                },
                cancellationToken).ConfigureAwait(false);

            await Console.Out.WriteLineAsync().ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Replay complete: {result.Total} total, {result.Succeeded} succeeded, {result.Failed} failed").ConfigureAwait(false);

            return result.Failed > 0 ? 1 : 0;
        }

        private IRequestSource CreateSource(string source, ParseResult parseResult)
        {
            return source.ToLowerInvariant() switch
            {
                "azure" => CreateAzureSource(parseResult),
                "file" => CreateFileSource(parseResult),
                "s3" => CreateS3Source(parseResult),
                "redis" => CreateRedisSource(parseResult),
                _ => throw new InvalidOperationException($"Unknown source '{source}'. Supported: azure, file, s3, redis.")
            };
        }

        private IRequestSource CreateAzureSource(ParseResult parseResult)
        {
            var connectionString = parseResult.GetValue(_connectionString)
                ?? throw new InvalidOperationException("--connection-string is required for source=azure.");
            var container = parseResult.GetValue(_container)!;
            var prefix = parseResult.GetValue(_prefix);

            var options = new AzureBlobStorageSinkOptions
            {
                ConnectionString = connectionString,
                ContainerName = container,
                BlobPrefix = prefix
            };

            var containerClient = new BlobContainerClient(connectionString, container);
            return new AzureBlobStorageSink(containerClient, options, NullLogger<AzureBlobStorageSink>.Instance);
        }

        private IRequestSource CreateFileSource(ParseResult parseResult)
        {
            var directory = parseResult.GetValue(_directory)
                ?? throw new InvalidOperationException("--directory is required for source=file.");

            var options = new FileSinkOptions { OutputDirectory = directory };
            return new FileSink(options, NullLogger<FileSink>.Instance);
        }

        private IRequestSource CreateS3Source(ParseResult parseResult)
        {
            var bucket = parseResult.GetValue(_bucket)
                ?? throw new InvalidOperationException("--bucket is required for source=s3.");
            var region = parseResult.GetValue(_region);
            var serviceUrl = parseResult.GetValue(_serviceUrl);

            var options = new AmazonS3SinkOptions
            {
                BucketName = bucket,
                Region = region,
                ServiceUrl = serviceUrl
            };

            var s3Config = new AmazonS3Config();
            if (!string.IsNullOrEmpty(serviceUrl))
            {
                s3Config.ServiceURL = serviceUrl;
                s3Config.ForcePathStyle = true;
            }
            else if (!string.IsNullOrEmpty(region))
            {
                s3Config.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
            }

            var s3Client = new AmazonS3Client(s3Config);
            return new AmazonS3Sink(s3Client, options, NullLogger<AmazonS3Sink>.Instance);
        }

        private IRequestSource CreateRedisSource(ParseResult parseResult)
        {
            var redisConnection = parseResult.GetValue(_redisConnection)
                ?? throw new InvalidOperationException("--redis-connection is required for source=redis.");
            var streamKey = parseResult.GetValue(_streamKey)!;

            var options = new RedisSinkOptions
            {
                ConnectionString = redisConnection,
                StreamKey = streamKey
            };

            var multiplexer = ConnectionMultiplexer.Connect(redisConnection);
            return new RedisSink(multiplexer, options, NullLogger<RedisSink>.Instance);
        }
    }
}

