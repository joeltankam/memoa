using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Headers;
using System.Text;
using Azure.Storage.Blobs;
using Memoa.Sinks.AzureBlobStorage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Memoa.Replay.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var connectionStringOption = new Option<string>("--connection-string", ["-c"]) { Description = "Azure Storage connection string.", Required = true };
        var containerOption = new Option<string>("--container") { Description = "Blob container name.", DefaultValueFactory = _ => "memoa-requests" };
        var prefixOption = new Option<string?>("--prefix") { Description = "Blob prefix to filter requests." };
        var targetOption = new Option<string>("--target", ["-t"]) { Description = "Base URL to replay requests against.", Required = true };
        var fromOption = new Option<DateTimeOffset?>("--from") { Description = "Only replay requests captured after this UTC time." };
        var toOption = new Option<DateTimeOffset?>("--to") { Description = "Only replay requests captured before this UTC time." };
        var methodsOption = new Option<string[]?>("--methods") { Description = "Only replay these HTTP methods." };
        var pathPatternOption = new Option<string?>("--path") { Description = "Glob pattern to filter request paths." };
        var dryRunOption = new Option<bool>("--dry-run") { Description = "Print requests without sending them." };
        var parallelismOption = new Option<int>("--parallelism") { Description = "Number of concurrent requests.", DefaultValueFactory = _ => 1 };
        var delayOption = new Option<int>("--delay") { Description = "Delay between requests in milliseconds.", DefaultValueFactory = _ => 0 };

        var rootCommand = new RootCommand("Replay HTTP requests captured by Memoa middleware.")
        {
            connectionStringOption,
            containerOption,
            prefixOption,
            targetOption,
            fromOption,
            toOption,
            methodsOption,
            pathPatternOption,
            dryRunOption,
            parallelismOption,
            delayOption
        };

        rootCommand.Action = new ReplayAction(
            connectionStringOption, containerOption, prefixOption, targetOption,
            fromOption, toOption, methodsOption, pathPatternOption,
            dryRunOption, parallelismOption, delayOption);

        var config = new CommandLineConfiguration(rootCommand);
        return await config.InvokeAsync(args).ConfigureAwait(false);
    }

    private sealed class ReplayAction : AsynchronousCommandLineAction
    {
        private readonly Option<string> _connectionString;
        private readonly Option<string> _container;
        private readonly Option<string?> _prefix;
        private readonly Option<string> _target;
        private readonly Option<DateTimeOffset?> _from;
        private readonly Option<DateTimeOffset?> _to;
        private readonly Option<string[]?> _methods;
        private readonly Option<string?> _pathPattern;
        private readonly Option<bool> _dryRun;
        private readonly Option<int> _parallelism;
        private readonly Option<int> _delay;

        public ReplayAction(
            Option<string> connectionString, Option<string> container,
            Option<string?> prefix, Option<string> target,
            Option<DateTimeOffset?> from, Option<DateTimeOffset?> to,
            Option<string[]?> methods, Option<string?> pathPattern,
            Option<bool> dryRun, Option<int> parallelism, Option<int> delay)
        {
            _connectionString = connectionString;
            _container = container;
            _prefix = prefix;
            _target = target;
            _from = from;
            _to = to;
            _methods = methods;
            _pathPattern = pathPattern;
            _dryRun = dryRun;
            _parallelism = parallelism;
            _delay = delay;
        }

        public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
        {
            return await RunReplayAsync(
                parseResult.GetValue(_connectionString)!,
                parseResult.GetValue(_container)!,
                parseResult.GetValue(_prefix),
                parseResult.GetValue(_target)!,
                parseResult.GetValue(_from),
                parseResult.GetValue(_to),
                parseResult.GetValue(_methods),
                parseResult.GetValue(_pathPattern),
                parseResult.GetValue(_dryRun),
                parseResult.GetValue(_parallelism),
                parseResult.GetValue(_delay),
                cancellationToken).ConfigureAwait(false);
        }
    }

    internal static async Task<int> RunReplayAsync(
        string connectionString,
        string container,
        string? prefix,
        string target,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string[]? methods,
        string? pathPattern,
        bool dryRun,
        int parallelism,
        int delayMs,
        CancellationToken cancellationToken)
    {
        var options = new AzureBlobStorageSinkOptions
        {
            ConnectionString = connectionString,
            ContainerName = container,
            BlobPrefix = prefix
        };

        var containerClient = new BlobContainerClient(connectionString, container);
        var source = new AzureBlobStorageSink(containerClient, options, NullLogger<AzureBlobStorageSink>.Instance);

        var query = new RequestQuery
        {
            From = from,
            To = to,
            Methods = methods,
            PathPattern = pathPattern
        };

        using var httpClient = new HttpClient { BaseAddress = new Uri(target) };

        var total = 0;
        var succeeded = 0;
        var failed = 0;

        var semaphore = new SemaphoreSlim(Math.Max(1, parallelism));
        var tasks = new List<Task>();

        await foreach (var request in source.ReadAsync(query, cancellationToken).ConfigureAwait(false))
        {
            Interlocked.Increment(ref total);

            if (dryRun)
            {
                await Console.Out.WriteLineAsync($"[DRY-RUN] {request.Method} {request.Path}{request.QueryString ?? ""}").ConfigureAwait(false);
                continue;
            }

            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await ReplayRequestAsync(httpClient, request, cancellationToken).ConfigureAwait(false);
                    Interlocked.Increment(ref succeeded);
                    await Console.Out.WriteLineAsync($"[OK] {request.Method} {request.Path} ({request.Id})").ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Interlocked.Increment(ref failed);
                    await Console.Error.WriteLineAsync($"[FAIL] {request.Method} {request.Path} ({request.Id}): {ex.Message}").ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }

                if (delayMs > 0)
                {
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Console.Out.WriteLineAsync().ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"Replay complete: {total} total, {succeeded} succeeded, {failed} failed").ConfigureAwait(false);

        return failed > 0 ? 1 : 0;
    }

    private static async Task ReplayRequestAsync(
        HttpClient httpClient,
        RecordedRequest request,
        CancellationToken cancellationToken)
    {
        var method = new HttpMethod(request.Method);
        var uri = $"{request.Path}{request.QueryString ?? ""}";
        using var message = new HttpRequestMessage(method, uri);

        if (request.Headers is not null)
        {
            foreach (var (key, values) in request.Headers)
            {
                if (key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                message.Headers.TryAddWithoutValidation(key, values);
            }
        }

        if (request.Body is not null)
        {
            if (request.Body.Text is not null)
            {
                message.Content = new StringContent(request.Body.Text, Encoding.UTF8);
            }
            else if (request.Body.Base64Bytes is not null)
            {
                message.Content = new ByteArrayContent(Convert.FromBase64String(request.Body.Base64Bytes));
            }

            if (message.Content is not null && request.Body.ContentType is not null)
            {
                message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(request.Body.ContentType);
            }
        }

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }
}

