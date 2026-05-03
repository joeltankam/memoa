using System.CommandLine;
using Amazon;
using Amazon.S3;
using Memoa.Sinks.AmazonS3;
using Microsoft.Extensions.Logging.Abstractions;

namespace Memoa.Replay.Cli.Sources;

internal sealed class AmazonS3SourceProvider : IReplaySourceProvider
{
    private readonly Option<string?> _bucket = new("--bucket") { Description = "S3 bucket name (source=s3)." };
    private readonly Option<string?> _region = new("--region") { Description = "AWS region (source=s3)." };
    private readonly Option<string?> _serviceUrl = new("--service-url") { Description = "S3-compatible service URL (source=s3)." };

    public string Name => "s3";
    public string Description => "Amazon S3 / S3-compatible";

    public IEnumerable<Option> GetOptions()
    {
        yield return _bucket;
        yield return _region;
        yield return _serviceUrl;
    }

    public IRequestSource CreateSource(ParseResult parseResult)
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
}
