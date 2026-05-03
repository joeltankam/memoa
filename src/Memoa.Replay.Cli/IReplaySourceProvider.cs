using System.CommandLine;

namespace Memoa.Replay.Cli;

/// <summary>
/// Defines a source provider that contributes CLI options and creates an <see cref="IRequestSource"/>.
/// Implement this interface to add a new replay source to the CLI.
/// </summary>
internal interface IReplaySourceProvider
{
    /// <summary>
    /// The source name used with <c>--source</c> (e.g., "azure", "file", "s3", "redis").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// A short description of the source for help text.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Returns the CLI options specific to this source.
    /// These are registered on the root command automatically.
    /// </summary>
    IEnumerable<Option> GetOptions();

    /// <summary>
    /// Creates an <see cref="IRequestSource"/> from the parsed CLI arguments.
    /// </summary>
    /// <param name="parseResult">The parsed command-line result.</param>
    /// <returns>A configured request source.</returns>
    /// <exception cref="InvalidOperationException">When required options are missing.</exception>
    IRequestSource CreateSource(ParseResult parseResult);
}
