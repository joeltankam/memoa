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

    [TestCase("badvalue")]
    [TestCase(":novalue")]
    public async Task Main_ShouldReturnNonZeroAndReportError_WhenAuthHeaderIsMalformed(string authHeader)
    {
        var originalError = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);

        try
        {
            var exitCode = await Program.Main(
            [
                "--source", "file",
                "--directory", Path.Combine(Path.GetTempPath(), "memoa-replay-tests"),
                "--target", "https://localhost:5001",
                "--auth-header", authHeader
            ]);

            exitCode.Should().Be(1);
            sw.ToString().Should().Contain("Error: --auth-header must be in 'Name:Value' format");
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [TestCase("notaurl")]
    [TestCase("/relative/path")]
    [TestCase("localhost:5000")]
    public async Task Main_ShouldReturnNonZeroAndReportError_WhenTargetIsNotAnAbsoluteHttpUrl(string target)
    {
        var originalError = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);

        try
        {
            var exitCode = await Program.Main(
            [
                "--source", "file",
                "--directory", Path.Combine(Path.GetTempPath(), "memoa-replay-tests"),
                "--target", target
            ]);

            exitCode.Should().Be(1);
            sw.ToString().Should().Contain("Error: --target must be an absolute http or https URL");
        }
        finally
        {
            Console.SetError(originalError);
        }
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
            output.Should().Contain("--auth-token");
            output.Should().Contain("--auth-header");
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
