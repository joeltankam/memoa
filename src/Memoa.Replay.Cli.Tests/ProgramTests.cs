using FluentAssertions;
using NUnit.Framework;

namespace Memoa.Replay.Cli.Tests;

[TestFixture(TestOf = typeof(Program))]
internal class ProgramTests
{
    [Test]
    public async Task Main_ShouldReturnNonZero_WhenRequiredOptionsAreMissing()
    {
        // Arrange & Act
        var exitCode = await Program.Main([]);

        // Assert
        exitCode.Should().NotBe(0);
    }

    [Test]
    public async Task Main_ShouldShowHelp_WhenHelpFlagIsUsed()
    {
        // Arrange
        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        try
        {
            // Act
            var exitCode = await Program.Main(["--help"]);

            // Assert
            exitCode.Should().Be(0);
            var output = sw.ToString();
            output.Should().Contain("--connection-string");
            output.Should().Contain("--target");
            output.Should().Contain("--dry-run");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
