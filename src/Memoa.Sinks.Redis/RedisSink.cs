using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Memoa.Sinks.Redis;

/// <summary>
/// Writes captured HTTP requests to a Redis Stream.
/// </summary>
public sealed class RedisSink : IRequestSink, IRequestSource
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisSinkOptions _options;
    private readonly ILogger<RedisSink> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions ReadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public RedisSink(IConnectionMultiplexer redis, RedisSinkOptions options, ILogger<RedisSink> logger)
    {
        _redis = redis;
        _options = options;
        _logger = logger;
    }

    public async ValueTask WriteAsync(RecordedRequest request, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase(_options.Database);
        var streamKey = GetStreamKey();

        var json = JsonSerializer.Serialize(request, JsonOptions);

        var entries = new NameValueEntry[]
        {
            new("id", request.Id.ToString()),
            new("timestamp", request.CapturedAtUtc.ToUnixTimeMilliseconds().ToString()),
            new("method", request.Method),
            new("path", request.Path),
            new("data", json)
        };

        if (_options.MaxLength.HasValue)
        {
            await db.StreamAddAsync(streamKey, entries, maxLength: _options.MaxLength.Value, useApproximateMaxLength: true).ConfigureAwait(false);
        }
        else
        {
            await db.StreamAddAsync(streamKey, entries).ConfigureAwait(false);
        }

        _logger.LogDebug("Wrote captured request {RequestId} to Redis stream {StreamKey}", request.Id, streamKey);
    }

    public async IAsyncEnumerable<RecordedRequest> ReadAsync(
        RequestQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase(_options.Database);
        var streamKey = GetStreamKey();

        // Determine the start ID based on 'From' time
        RedisValue startId = "-";
        if (query.From.HasValue)
        {
            startId = $"{query.From.Value.ToUnixTimeMilliseconds()}-0";
        }

        RedisValue endId = "+";
        if (query.To.HasValue)
        {
            endId = $"{query.To.Value.ToUnixTimeMilliseconds()}-0";
        }

        var entries = await db.StreamRangeAsync(streamKey, startId, endId).ConfigureAwait(false);

        if (entries is null)
        {
            yield break;
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dataField = Array.Find(entry.Values, v => v.Name == "data");
            if (dataField.Value.IsNullOrEmpty)
            {
                continue;
            }

            RecordedRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<RecordedRequest>(dataField.Value.ToString(), ReadJsonOptions);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to deserialize Redis stream entry {EntryId}, skipping", entry.Id);
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

    private string GetStreamKey()
    {
        if (string.IsNullOrEmpty(_options.KeyPrefix))
        {
            return _options.StreamKey;
        }

        return $"{_options.KeyPrefix}:{_options.StreamKey}";
    }

    private static bool MatchesQuery(RecordedRequest request, RequestQuery query)
    {
        if (query.Methods is { Count: > 0 } &&
            !query.Methods.Contains(request.Method, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
