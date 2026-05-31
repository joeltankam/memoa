using Memoa;
using Memoa.Sinks.AzureBlobStorage;

var builder = WebApplication.CreateBuilder(args);

// Register Memoa with Azure Blob Storage sink (using Azurite locally)
builder.Services
    .AddMemoa(opts =>
    {
        opts.Capture.IncludeHeaders = true;
        opts.Capture.IncludeBody = true;
        opts.Capture.IncludeResponse = true;
        opts.Capture.IncludeResponseBody = true;
        opts.Pipeline.Mode = PipelineMode.Background;
    })
    .WriteTo.AzureBlobStorage(
        builder.Configuration.GetConnectionString("AzureStorage")
            ?? "UseDevelopmentStorage=true",
        opts =>
        {
            opts.ContainerName = "memoa-sample";
        });

var app = builder.Build();

app.UseMemoa();

app.MapGet("/", () => "Memoa Sample API");

app.MapGet("/api/hello", () => Results.Ok(new { Message = "Hello, World!" }));

app.MapPost("/api/echo", async (HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync().ConfigureAwait(false);
    return Results.Ok(new { Echo = body });
});

app.MapGet("/health", () => Results.Ok("Healthy"));

await app.RunAsync().ConfigureAwait(false);
