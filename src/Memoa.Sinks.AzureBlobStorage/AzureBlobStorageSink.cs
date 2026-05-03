using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;

namespace Memoa.Sinks.AzureBlobStorage;

/// <summary>
/// Writes captured HTTP requests to Azure Blob Storage as JSON blobs.
/// </summary>
public sealed class AzureBlobStorageSink : IRequestSink, IRequestSource
{
    private readonly BlobContainerClient _containerClient;
    private readonly AzureBlobStorageSinkOptions _options;
    private readonly ILogger<AzureBlobStorageSink> _logger;
    private bool _containerEnsured;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AzureBlobStorageSink(
        BlobContainerClient containerClient,
        AzureBlobStorageSinkOptions options,
        ILogger<AzureBlobStorageSink> logger)
    {
        _containerClient = containerClient;
        _options = options;
        _logger = logger;
    }

    public async ValueTask WriteAsync(RecordedRequest request, CancellationToken cancellationToken)
    {
        await EnsureContainerAsync(cancellationToken).ConfigureAwait(false);

        var blobName = FormatBlobName(request);
        var blobClient = _containerClient.GetBlobClient(blobName);

        var json = JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);
        using var stream = new MemoryStream(json);

        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Wrote captured request {RequestId} to blob {BlobName}", request.Id, blobName);
    }

    public async IAsyncEnumerable<RecordedRequest> ReadAsync(
        RequestQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await EnsureContainerAsync(cancellationToken).ConfigureAwait(false);

        var prefix = BuildPrefix(query);

        await foreach (var blobItem in _containerClient.GetBlobsAsync(
            traits: Azure.Storage.Blobs.Models.BlobTraits.None,
            states: Azure.Storage.Blobs.Models.BlobStates.None,
            prefix: prefix,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            RecordedRequest? request;
            try
            {
                var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                var response = await blobClient.DownloadContentAsync(cancellationToken).ConfigureAwait(false);
                request = JsonSerializer.Deserialize<RecordedRequest>(
                    response.Value.Content.ToMemory().Span, JsonOptions);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to deserialize blob {BlobName}, skipping", blobItem.Name);
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

    private string FormatBlobName(RecordedRequest request)
    {
        var ts = request.CapturedAtUtc;
        var name = _options.BlobNameFormat
            .Replace("{year}", ts.Year.ToString("D4"), StringComparison.OrdinalIgnoreCase)
            .Replace("{month}", ts.Month.ToString("D2"), StringComparison.OrdinalIgnoreCase)
            .Replace("{day}", ts.Day.ToString("D2"), StringComparison.OrdinalIgnoreCase)
            .Replace("{hour}", ts.Hour.ToString("D2"), StringComparison.OrdinalIgnoreCase)
            .Replace("{id}", request.Id.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{method}", request.Method, StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(_options.BlobPrefix))
        {
            name = $"{_options.BlobPrefix.TrimEnd('/')}/{name}";
        }

        return name;
    }

    private string? BuildPrefix(RequestQuery query)
    {
        var prefix = _options.BlobPrefix;

        if (query.From.HasValue)
        {
            var ts = query.From.Value;
            var datePrefix = $"{ts.Year:D4}/{ts.Month:D2}/{ts.Day:D2}";
            prefix = string.IsNullOrEmpty(prefix) ? datePrefix : $"{prefix.TrimEnd('/')}/{datePrefix}";
        }

        return prefix;
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

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_containerEnsured)
        {
            return;
        }

        if (_options.CreateContainerIfNotExists)
        {
            await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        _containerEnsured = true;
    }
}
