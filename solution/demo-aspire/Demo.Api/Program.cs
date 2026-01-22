using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using System.Collections.Concurrent;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
var serviceName = "demo-api";
var serviceVersion = "1.0.0";
var otlp = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
           ?? "http://otel-opentelemetry-collector.observability.svc.cluster.local:4317";

// Memory endpoint configuration: designed to exceed pod resource limits and trigger OOMKill
// These constants control the rate and volume of memory allocation to demonstrate pod restart behavior
const int MemoryChunkSizeBytes = 20 * 1024 * 1024; // 20 MiB per allocation chunk
const int MemoryChunkCount = 10; // Total of 10 chunks = ~200 MiB (exceeds 100Mi pod limit)
const int MemoryDelayMs = 100; // ~1 second total duration with 100ms delay per chunk

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
    
    // Configure resource for logs
    logging.SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService(serviceName, serviceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["service.instance.id"] = Environment.MachineName
        }));
    
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

app.MapGet("/memory", async (ILogger<Program> logger) =>
{
    // Force GC collection to clear available memory before aggressive allocation
    // This ensures the endpoint will reliably exceed the 100Mi pod limit and trigger OOMKill
    GC.Collect();
    GC.WaitForPendingFinalizers();
    
    var targetMiB = MemoryChunkSizeBytes * MemoryChunkCount / (1024 * 1024);
    logger.LogWarning("Memory consumption endpoint invoked - will allocate ~{TargetMiB}MiB to trigger OOMKill", targetMiB);
    
    try
    {
        // Allocate chunks synchronously in the request handler to ensure retention
        // Each chunk is 20MiB; pod OOMKill typically triggers around chunk 5-6 (exceeding 100Mi limit)
        for (var i = 0; i < MemoryChunkCount; i++)
        {
            // Allocate uninitialized array and touch all 4KB pages to force physical memory commitment
            var buffer = GC.AllocateUninitializedArray<byte>(MemoryChunkSizeBytes);
            for (var j = 0; j < buffer.Length; j += 4096)
                buffer[j] = 0xFF; // Write to each page to commit physical memory
            
            // Store reference in static class to prevent GC reclamation during pod lifetime
            MemoryHolder.RetainedBuffers.Add(buffer);
            logger.LogInformation("Memory chunk {Current}/{Total} allocated ({SizeMiB}MiB)", i + 1, MemoryChunkCount, MemoryChunkSizeBytes / (1024 * 1024));
            
            // Delay provides observation window for logs/telemetry before pod restart
            await Task.Delay(MemoryDelayMs);
        }
        
        return Results.Ok(new { message = "Starting consuming memory...", status = "allocated", targetMiB, note = "Pod will restart when 100Mi limit is exceeded" });
    }
    catch (OutOfMemoryException ex)
    {
        logger.LogError("OutOfMemoryException caught during allocation: {Message}", ex.Message);
        return Results.StatusCode(500);
    }
});

app.Run();

// Static holder class for retained memory buffers to prevent garbage collection
// Prevents GC from reclaiming allocated memory during the pod's lifecycle until restart
static class MemoryHolder
{
    public static readonly List<byte[]> RetainedBuffers = new();
}