using FluentAssertions;
using Memoa.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;

namespace Memoa.Tests;

[TestFixture(TestOf = typeof(MemoaServiceCollectionExtensions))]
internal class MemoaServiceCollectionExtensionsTests
{
    [Test]
    public void AddMemoa_ShouldReturnBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        var builder = services.AddMemoa();

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeAssignableTo<IMemoaBuilder>();
        builder.WriteTo.Should().NotBeNull();
    }

    [Test]
    public void AddMemoa_ShouldConfigureOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddMemoa(opts =>
        {
            opts.Enabled = false;
            opts.CorrelationIdHeader = "X-Custom";
        });

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MemoaOptions>>().Value;

        // Assert
        options.Enabled.Should().BeFalse();
        options.CorrelationIdHeader.Should().Be("X-Custom");
    }

    [Test]
    public void AddMemoa_ShouldResolveInlineRequestPipeline_WhenModeIsInline()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMemoa(opts =>
        {
            opts.Pipeline.Mode = PipelineMode.Inline;
        });

        var sp = services.BuildServiceProvider();

        // Act
        var pipeline = sp.GetRequiredService<IRequestPipeline>();

        // Assert
        pipeline.Should().BeOfType<InlineRequestPipeline>();
    }

    [Test]
    public void AddMemoa_ShouldResolveBackgroundRequestPipeline_WhenModeIsBackground()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMemoa(opts =>
        {
            opts.Pipeline.Mode = PipelineMode.Background;
        });

        var sp = services.BuildServiceProvider();

        // Act
        var pipeline = sp.GetRequiredService<IRequestPipeline>();

        // Assert
        pipeline.Should().BeOfType<BackgroundRequestPipeline>();
    }

    [Test]
    public void AddMemoa_ShouldRegisterHostedService_WhenModeIsBackground()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoa();

        var sp = services.BuildServiceProvider();

        // Act
        var hostedServices = sp.GetServices<IHostedService>().ToList();

        // Assert
        hostedServices.Should().ContainSingle(s => s is BackgroundRequestPipeline);
    }

    [Test]
    public void WriteTo_ShouldExposeServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        var builder = services.AddMemoa();

        // Assert
        builder.WriteTo.Services.Should().BeSameAs(services);
    }

    [Test]
    public async Task AddMemoa_ShouldWorkWithTestServer()
    {
        // Arrange
        var sinkMock = new Mock<IRequestSink>(MockBehavior.Strict);
        sinkMock
            .Setup(s => s.WriteAsync(It.IsAny<RecordedRequest>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddMemoa(opts =>
                        {
                            opts.Pipeline.Mode = PipelineMode.Inline;
                        });
                        services.AddSingleton<IRequestSink>(sinkMock.Object);
                    })
                    .Configure(app =>
                    {
                        app.UseMemoa();
                        app.Run(static async ctx => await ctx.Response.WriteAsync("OK"));
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.EnsureSuccessStatusCode();
        sinkMock.Verify(
            s => s.WriteAsync(It.Is<RecordedRequest>(r => r.Path == "/api/test"), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
