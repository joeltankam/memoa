var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Memoa Sample API");

await app.RunAsync().ConfigureAwait(false);
