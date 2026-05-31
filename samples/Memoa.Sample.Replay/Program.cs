using Memoa;
using Memoa.Replay.Api;
using Memoa.Sinks.File;

var builder = WebApplication.CreateBuilder(args);

var captureDir = Path.Combine(builder.Environment.ContentRootPath, "captured-requests");

// Configure Memoa capture with file sink
builder.Services.AddMemoa(options =>
{
    options.Capture.IncludeBody = true;
})
.WriteTo.FileSystem(opts =>
{
    opts.OutputDirectory = captureDir;
});

// Configure replay API with file source (FileSink implements IRequestSource)
builder.Services.AddMemoaReplay(options =>
{
    options.ApiKey = "dev-api-key";
});
builder.Services.AddSingleton<IRequestSource>(
    sp => new FileSink(new FileSinkOptions { OutputDirectory = captureDir }, sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FileSink>>()));

var app = builder.Build();

app.UseMemoa();
app.MapMemoaReplay();

app.MapGet("/", () => "Hello! POST to /api/test to capture requests, then use /memoa/replay to replay them.");
app.MapPost("/api/test", () => Results.Ok(new { Status = "captured" }));

app.Run();
