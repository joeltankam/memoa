using System.CommandLine;
using Memoa.Sinks.Redis;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace Memoa.Replay.Cli.Sources;

internal sealed class RedisSourceProvider : IReplaySourceProvider
{
    private readonly Option<string?> _redisConnection = new("--redis-connection") { Description = "Redis connection string (source=redis)." };
    private readonly Option<string> _streamKey = new("--stream-key") { Description = "Redis stream key (source=redis).", DefaultValueFactory = _ => "memoa:requests" };

    public string Name => "redis";
    public string Description => "Redis Streams";

    public IEnumerable<Option> GetOptions()
    {
        yield return _redisConnection;
        yield return _streamKey;
    }

    public IRequestSource CreateSource(ParseResult parseResult)
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
