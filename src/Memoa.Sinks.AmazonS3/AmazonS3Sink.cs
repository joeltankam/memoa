using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;

namespace Memoa.Sinks.AmazonS3;

/// <summary>
/// Writes captured HTTP requests to Amazon S3 as JSON objects.
/// </summary>
public sealed class AmazonS3Sink : IRequestSink, IRequestSource
{
    private readonly IAmazonS3 _s3Client;
    private readonly AmazonS3SinkOptions _options;
    private readonly ILogger<AmazonS3Sink> _logger;
    private bool _bucketEnsured;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions ReadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public AmazonS3Sink(IAmazonS3 s3Client, AmazonS3SinkOptions options, ILogger<AmazonS3Sink> logger)
    {
        _s3Client = s3Client;
        _options = options;
        _logger = logger;
    }

    public async ValueTask WriteAsync(RecordedRequest request, CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken).ConfigureAwait(false);

        var key = FormatKey(request);
        var json = JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);

        using var stream = new MemoryStream(json);
        var putRequest = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = stream,
            ContentType = "application/json"
        };

        await _s3Client.PutObjectAsync(putRequest, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Wrote captured request {RequestId} to S3 key {Key}", request.Id, key);
    }

    public async IAsyncEnumerable<RecordedRequest> ReadAsync(
        RequestQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken).ConfigureAwait(false);

        var prefix = BuildPrefix(query);
        string? continuationToken = null;

        do
        {
            var listRequest = new ListObjectsV2Request
            {
                BucketName = _options.BucketName,
                Prefix = prefix,
                ContinuationToken = continuationToken
            };

            var listResponse = await _s3Client.ListObjectsV2Async(listRequest, cancellationToken).ConfigureAwait(false);

            foreach (var s3Object in listResponse.S3Objects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                RecordedRequest? request;
                try
                {
                    var getRequest = new GetObjectRequest
                    {
                        BucketName = _options.BucketName,
                        Key = s3Object.Key
                    };

                    using var response = await _s3Client.GetObjectAsync(getRequest, cancellationToken).ConfigureAwait(false);
                    using var responseStream = response.ResponseStream;
                    request = await JsonSerializer.DeserializeAsync<RecordedRequest>(
                        responseStream, ReadJsonOptions, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Failed to read S3 object {Key}, skipping", s3Object.Key);
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

            continuationToken = listResponse.IsTruncated == true ? listResponse.NextContinuationToken : null;
        }
        while (continuationToken is not null);
    }

    private string FormatKey(RecordedRequest request)
    {
        var ts = request.CapturedAtUtc;
        var key = _options.KeyFormat
            .Replace("{year}", ts.Year.ToString("D4"), StringComparison.OrdinalIgnoreCase)
            .Replace("{month}", ts.Month.ToString("D2"), StringComparison.OrdinalIgnoreCase)
            .Replace("{day}", ts.Day.ToString("D2"), StringComparison.OrdinalIgnoreCase)
            .Replace("{hour}", ts.Hour.ToString("D2"), StringComparison.OrdinalIgnoreCase)
            .Replace("{id}", request.Id.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{method}", request.Method, StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(_options.KeyPrefix))
        {
            key = $"{_options.KeyPrefix.TrimEnd('/')}/{key}";
        }

        return key;
    }

    private string? BuildPrefix(RequestQuery query)
    {
        var prefix = _options.KeyPrefix;

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

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (_bucketEnsured)
        {
            return;
        }

        if (_options.CreateBucketIfNotExists)
        {
            try
            {
                await _s3Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = _options.BucketName
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (AmazonS3Exception ex) when (
                ex.ErrorCode == "BucketAlreadyOwnedByYou" ||
                ex.ErrorCode == "BucketAlreadyExists")
            {
                // Bucket already exists, that's fine
            }
        }

        _bucketEnsured = true;
    }
}
