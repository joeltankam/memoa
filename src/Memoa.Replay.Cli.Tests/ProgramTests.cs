using FluentAssertions;
using NUnit.Framework;

namespace Memoa.Replay.Cli.Tests;

[TestFixture(TestOf = typeof(Program))]
internal class ProgramTests
{
    [Test]
    public async Task Main_ShouldReturnNonZero_WhenRequiredOptionsAreMissing()
    {
        var exitCode = await Program.Main([]);

        exitCode.Should().NotBe(0);
    }

    [Test]
    public async Task Main_ShouldShowHelp_WhenHelpFlagIsUsed()
    {
        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        try
        {
            var exitCode = await Program.Main(["--help"]);

            exitCode.Should().Be(0);
            var output = sw.ToString();
            output.Should().Contain("--source");
            output.Should().Contain("--target");
            output.Should().Contain("--timeline");
            output.Should().Contain("--dry-run");
            output.Should().Contain("--connection-string");
            output.Should().Contain("--directory");
            output.Should().Contain("--bucket");
            output.Should().Contain("--redis-connection");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
