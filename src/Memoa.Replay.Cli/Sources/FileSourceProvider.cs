using System.CommandLine;
using Memoa.Sinks.File;
using Microsoft.Extensions.Logging.Abstractions;

namespace Memoa.Replay.Cli.Sources;

internal sealed class FileSourceProvider : IReplaySourceProvider
{
    private readonly Option<string?> _directory = new("--directory", ["-d"]) { Description = "Directory path (source=file)." };

    public string Name => "file";
    public string Description => "Local file system";

    public IEnumerable<Option> GetOptions()
    {
        yield return _directory;
    }

    public IRequestSource CreateSource(ParseResult parseResult)
    {
        var directory = parseResult.GetValue(_directory)
            ?? throw new InvalidOperationException("--directory is required for source=file.");

        var options = new FileSinkOptions { OutputDirectory = directory };
        return new FileSink(options, NullLogger<FileSink>.Instance);
    }
}
