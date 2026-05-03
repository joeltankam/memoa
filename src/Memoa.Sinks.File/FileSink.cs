using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Memoa.Sinks.File;

/// <summary>
/// Writes captured HTTP requests to the local file system as JSON files.
/// </summary>
public sealed class FileSink : IRequestSink, IRequestSource
{
    private readonly FileSinkOptions _options;
    private readonly ILogger<FileSink> _logger;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions CompactWriteOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public FileSink(FileSinkOptions options, ILogger<FileSink> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async ValueTask WriteAsync(RecordedRequest request, CancellationToken cancellationToken)
    {
        var relativePath = FormatFileName(request);
        var fullPath = Path.Combine(_options.OutputDirectory, relativePath);

        var directory = Path.GetDirectoryName(fullPath);
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var jsonOptions = _options.IndentJson ? WriteOptions : CompactWriteOptions;
        var json = JsonSerializer.SerializeToUtf8Bytes(request, jsonOptions);

#if NET8_0_OR_GREATER
        await System.IO.File.WriteAllBytesAsync(fullPath, json, cancellationToken).ConfigureAwait(false);
#else
        using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await fs.WriteAsync(json, 0, json.Length, cancellationToken).ConfigureAwait(false);
#endif

        _logger.LogDebug("Wrote captured request {RequestId} to {FilePath}", request.Id, fullPath);
    }

    public async IAsyncEnumerable<RecordedRequest> ReadAsync(
        RequestQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rootDir = _options.OutputDirectory;
        if (!Directory.Exists(rootDir))
        {
            yield break;
        }

        var files = Directory.EnumerateFiles(rootDir, "*.json", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RecordedRequest? request;
            try
            {
#if NET8_0_OR_GREATER
                var bytes = await System.IO.File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
#else
                byte[] bytes;
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
                {
                    bytes = new byte[fs.Length];
                    await fs.ReadAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
                }
#endif
                request = JsonSerializer.Deserialize<RecordedRequest>(bytes, ReadOptions);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to deserialize file {FilePath}, skipping", file);
                continue;
            }

            if (request is null)
            {
                continue;
            }

            if (!MatchesQuery(request, query))
            {
                continue;
            }

            yield return request;
        }
    }

    private string FormatFileName(RecordedRequest request)
    {
        var ts = request.CapturedAtUtc;
        return _options.FileNameFormat
            .Replace("{year}", ts.Year.ToString("D4"), StringComparison.OrdinalIgnoreCase)
            .Replace("{month}", ts.Month.ToString("D2"), StringComparison.OrdinalIgnoreCase)
            .Replace("{day}", ts.Day.ToString("D2"), StringComparison.OrdinalIgnoreCase)
            .Replace("{hour}", ts.Hour.ToString("D2"), StringComparison.OrdinalIgnoreCase)
            .Replace("{id}", request.Id.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{method}", request.Method, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesQuery(RecordedRequest request, RequestQuery query)
    {
        if (query.From.HasValue && request.CapturedAtUtc < query.From.Value)
        {
            return false;
        }

        if (query.To.HasValue && request.CapturedAtUtc > query.To.Value)
        {
            return false;
        }

        if (query.Methods is { Count: > 0 } &&
            !query.Methods.Contains(request.Method, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(query.PathPattern) &&
            !GlobIsMatch(request.Path, query.PathPattern))
        {
            return false;
        }

        return true;
    }

    private static bool GlobIsMatch(string value, string pattern)
    {
        var escaped = Regex.Escape(pattern);
        escaped = escaped.Replace(@"\*\*", ".*");
        escaped = escaped.Replace(@"\*", "[^/]*");
        escaped = escaped.Replace(@"\?", ".");
        return Regex.IsMatch(value, $"^{escaped}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
