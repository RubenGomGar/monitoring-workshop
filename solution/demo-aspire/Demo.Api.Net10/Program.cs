using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// ActivitySource for custom traces
var serviceName = "demo-api-net10";
var serviceVersion = "1.0.0";
var activitySource = new ActivitySource(serviceName);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapControllers();
app.MapGet("/ping", (ILogger<Program> logger) =>
{
    using var activity = activitySource.StartActivity("ping-endpoint");
    activity?.SetTag("custom.endpoint", "ping");

    // Example log with different levels
    logger.LogInformation("Ping endpoint called at {Timestamp}", DateTimeOffset.UtcNow);

    return Results.Ok(new { ok = true, at = DateTimeOffset.UtcNow, version = serviceVersion });
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
