using System.CommandLine;
using Azure.Storage.Blobs;
using Memoa.Sinks.AzureBlobStorage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Memoa.Replay.Cli.Sources;

internal sealed class AzureBlobSourceProvider : IReplaySourceProvider
{
    private readonly Option<string?> _connectionString = new("--connection-string", ["-c"]) { Description = "Azure Storage connection string (source=azure)." };
    private readonly Option<string> _container = new("--container") { Description = "Blob container name (source=azure).", DefaultValueFactory = _ => "memoa-requests" };
    private readonly Option<string?> _prefix = new("--prefix") { Description = "Blob prefix (source=azure)." };

    public string Name => "azure";
    public string Description => "Azure Blob Storage";

    public IEnumerable<Option> GetOptions()
    {
        yield return _connectionString;
        yield return _container;
        yield return _prefix;
    }

    public IRequestSource CreateSource(ParseResult parseResult)
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
}
