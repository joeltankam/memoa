using FluentAssertions;
using Memoa.Sinks.File;
using NUnit.Framework;

namespace Memoa.Sinks.File.Tests;

[TestFixture(TestOf = typeof(FileSinkOptions))]
internal class FileSinkOptionsTests
{
    [Test]
    public void Defaults_ShouldHaveExpectedValues()
    {
        // Act
        var options = new FileSinkOptions();

        // Assert
        options.OutputDirectory.Should().Be("./memoa-requests");
        options.FileNameFormat.Should().Be("{year}/{month}/{day}/{hour}/{id}.json");
        options.IndentJson.Should().BeTrue();
    }

    [Test]
    public void OutputDirectory_CanBeChanged()
    {
        // Arrange
        var options = new FileSinkOptions();

        // Act
        options.OutputDirectory = "/custom/path";

        // Assert
        options.OutputDirectory.Should().Be("/custom/path");
    }

    [Test]
    public void FileNameFormat_CanBeChanged()
    {
        // Arrange
        var options = new FileSinkOptions();

        // Act
        options.FileNameFormat = "{method}/{id}.json";

        // Assert
        options.FileNameFormat.Should().Be("{method}/{id}.json");
    }

    [Test]
    public void IndentJson_CanBeDisabled()
    {
        // Arrange
        var options = new FileSinkOptions();

        // Act
        options.IndentJson = false;

        // Assert
        options.IndentJson.Should().BeFalse();
    }
}
