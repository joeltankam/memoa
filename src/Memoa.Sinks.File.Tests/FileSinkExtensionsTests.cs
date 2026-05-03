using FluentAssertions;
using Memoa.Sinks.File;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Memoa.Sinks.File.Tests;

[TestFixture(TestOf = typeof(FileSinkExtensions))]
internal class FileSinkExtensionsTests
{
    [Test]
    public void FileSystem_WithDirectory_ShouldRegisterFileSinkAsRequestSink()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddMemoa();

        // Act
        builder.WriteTo.FileSystem("/tmp/requests");

        var sp = services.BuildServiceProvider();
        var sinks = sp.GetServices<IRequestSink>().ToList();

        // Assert
        sinks.Should().ContainSingle();
        sinks[0].Should().BeOfType<FileSink>();
    }

    [Test]
    public void FileSystem_WithDirectory_ShouldApplyConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddMemoa();

        // Act
        builder.WriteTo.FileSystem("/tmp/requests", opts =>
        {
            opts.IndentJson = false;
            opts.FileNameFormat = "{id}.json";
        });

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<FileSinkOptions>();

        // Assert
        options.OutputDirectory.Should().Be("/tmp/requests");
        options.IndentJson.Should().BeFalse();
        options.FileNameFormat.Should().Be("{id}.json");
    }

    [Test]
    public void FileSystem_WithoutDirectory_ShouldUseDefaultDirectory()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddMemoa();

        // Act
        builder.WriteTo.FileSystem();

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<FileSinkOptions>();

        // Assert
        options.OutputDirectory.Should().Be("./memoa-requests");
    }

    [Test]
    public void FileSystem_WithoutDirectory_ShouldApplyConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddMemoa();

        // Act
        builder.WriteTo.FileSystem(opts =>
        {
            opts.OutputDirectory = "/custom";
            opts.IndentJson = false;
        });

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<FileSinkOptions>();

        // Assert
        options.OutputDirectory.Should().Be("/custom");
        options.IndentJson.Should().BeFalse();
    }

    [Test]
    public void FileSystem_ShouldReturnSinkBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddMemoa();

        // Act
        var result = builder.WriteTo.FileSystem("/tmp/requests");

        // Assert — returns same builder for chaining
        result.Should().BeSameAs(builder.WriteTo);
    }
}
