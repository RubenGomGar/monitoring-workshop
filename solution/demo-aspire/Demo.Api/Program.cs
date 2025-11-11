using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
var serviceName = "demo-api";
var serviceVersion = "1.0.0";
var otlp = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
           ?? "http://otel-opentelemetry-collector.observability.svc.cluster.local:4317";

// ActivitySource for custom traces
var activitySource = new ActivitySource(serviceName);

// Configure OpenTelemetry (Metrics, Traces, Logs)
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r
        .AddService(serviceName, serviceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["service.instance.id"] = Environment.MachineName
        }))
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation()
         .AddHttpClientInstrumentation()
         .AddRuntimeInstrumentation()
         .AddOtlpExporter(o => 
         {
             o.Endpoint = new Uri(otlp);
             o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
         });
    })
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation()
         .AddHttpClientInstrumentation()
         .AddSource(serviceName)
         .SetSampler(new TraceIdRatioBasedSampler(1.0))
         .AddOtlpExporter(o => 
         {
             o.Endpoint = new Uri(otlp);
             o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
         });
    });

// Configure Logging with OTLP
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeScopes = true;
    logging.IncludeFormattedMessage = true;
    logging.AddOtlpExporter(o =>
    {
        o.Endpoint = new Uri(otlp);
        o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
    });
});

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

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

app.Run();