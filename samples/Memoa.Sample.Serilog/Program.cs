using Memoa;
using Serilog;
using Serilog.Sinks.Memoa;

Log.Logger = new LoggerConfiguration()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoa(options =>
{
    options.Capture.IncludeBody = true;
    options.Capture.IncludeResponse = true;
    options.Capture.IncludeResponseBody = true;
})
.WriteTo.Serilog(Log.Logger, opts =>
{
    opts.IncludeRequestBody = true;
    opts.IncludeHeaders = true;
    opts.MaxBodyLength = 8192;
});

var app = builder.Build();

app.UseMemoa();

app.MapGet("/", () => "Hello, World!");
app.MapPost("/echo", (HttpRequest req) => Results.Ok(new { Message = "Echo" }));

app.Run();
