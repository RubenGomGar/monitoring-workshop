# 🚀 Workshop: Minimal Observability with **Minikube (Docker driver)** + **Prometheus/Grafana** + **OpenTelemetry** + **.NET (Aspire‑ready)**

This workshop sets up a **100% local** and **cloud‑agnostic** environment using **Minikube inside Docker** (no k3d or Rancher), including:
- Local Kubernetes (Minikube, Docker driver)
- Prometheus + Grafana (kube‑prometheus‑stack)
- OpenTelemetry Collector (OTLP → Prometheus)
- .NET app (Aspire‑ready) that exports metrics/traces via OTLP

> Goal: **the simplest possible path**, with no external registry and no complex network configuration.

---

## 1) Requirements

Check that you have these tools installed:

| Tool | Command |
|---|---|
| Docker Desktop/Engine | `docker ps` |
| Minikube | `minikube version` |
| kubectl | `kubectl version --client` |
| Helm | `helm version` |
| .NET 9 SDK | `dotnet --version` |

Quick Minikube install (Windows with Chocolatey):
```bash
choco install minikube -y
```
(On Linux/Mac use the official Minikube installation method.)

---

## 2) Start Minikube inside Docker

```bash
minikube start -p demo --driver=docker
kubectl get nodes

minikube -p demo dashboard
```
You should see 1 node `Ready`.

> Tip (Windows/macOS): make sure Docker is running before starting Minikube.

---

## 3) Build images directly in Minikube's Docker daemon

Avoid using a registry. Build inside Minikube's daemon:

```powershell
# In PowerShell (Windows):
& minikube -p demo docker-env | Invoke-Expression

# In Bash (Linux/Mac):
# eval $(minikube -p demo docker-env)

# Verify configuration:
docker images
# from here, any 'docker build' is stored inside Minikube
```

---

## 4) Install Prometheus + Grafana (kube‑prometheus‑stack)

```bash
kubectl create ns monitoring

helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update

helm install kps prometheus-community/kube-prometheus-stack -n monitoring --set grafana.service.type=NodePort
```

Wait for all pods to be ready:
```bash
kubectl -n monitoring get pods

# Wait until all show Ready (may take 2-3 minutes)
# You should see: alertmanager, grafana, kube-state-metrics, node-exporter, prometheus-operator, prometheus-server
```

Get the Grafana URL:
```bash
minikube -p demo service -n monitoring kps-grafana --url
```

Grafana password (user: `admin`):

```bash
# Linux/macOS/Git Bash:
kubectl -n monitoring get secret kps-grafana -o jsonpath="{.data.admin-password}" | base64 -d; echo
```

```powershell
# PowerShell:
kubectl -n monitoring get secret kps-grafana -o jsonpath="{.data.admin-password}" | ForEach-Object { [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($_)) }
```

> An alternative way to get this secret would be going to the Kubernetes Dashboard, navigate to Secrets -> kps-grafana -> admin-password (👁️)

> Note: kube‑prometheus‑stack already configures Prometheus Operator, Alertmanager and Grafana with default Kubernetes dashboards.

---

## 5) Install Grafana Tempo (for traces)

Create `tempo-values.yaml`:

```yaml
tempo:
  reportingEnabled: false
  metricsGenerator:
    enabled: true
    remoteWriteUrl: "http://kps-kube-prometheus-st-prometheus.monitoring:9090/api/v1/write"

persistence:
  enabled: true
  size: 10Gi

serviceMonitor:
  enabled: true
  labels:
    release: kps

# Optimized resources for demo
resources:
  limits:
    cpu: 500m
    memory: 512Mi
  requests:
    cpu: 100m
    memory: 128Mi
```

Install Tempo:
```bash
kubectl create ns observability

helm repo add grafana https://grafana.github.io/helm-charts
helm repo update

helm install tempo grafana/tempo -n observability -f tempo-values.yaml
```

Verify Tempo is ready:
```bash
kubectl -n observability get pods | findstr tempo
```

---

## 6) Install Grafana Loki (for logs)

Create `loki-values.yaml`:

```yaml
loki:
  auth_enabled: false
  commonConfig:
    replication_factor: 1
  storage:
    type: 'filesystem'
  
  # Required schema configuration
  schemaConfig:
    configs:
      - from: 2024-01-01
        store: tsdb
        object_store: filesystem
        schema: v13
        index:
          prefix: index_
          period: 24h

singleBinary:
  replicas: 1
  persistence:
    enabled: true
    size: 10Gi
  resources:
    limits:
      cpu: 500m
      memory: 512Mi
    requests:
      cpu: 100m
      memory: 128Mi

# Disable unnecessary components for local demo
backend:
  replicas: 0
read:
  replicas: 0
write:
  replicas: 0

# Use single binary mode
deploymentMode: SingleBinary

monitoring:
  serviceMonitor:
    enabled: true
    labels:
      release: kps

  selfMonitoring:
    enabled: false
    grafanaAgent:
      installOperator: false
```

Install Loki:
```bash
helm install loki grafana/loki -n observability -f loki-values.yaml
```

Verify Loki is ready:
```bash
kubectl -n observability get pods | findstr loki
```

---

## 7) Install OpenTelemetry Collector (gateway for metrics, traces & logs)

Create `otel-values.yaml` with this content:

```yaml
mode: deployment
replicaCount: 1

image:
  repository: otel/opentelemetry-collector-contrib
  tag: 0.112.0
  pullPolicy: IfNotPresent

service:
  type: ClusterIP

serviceMonitor:
  enabled: true
  extraLabels:
    release: kps
  metricsEndpoints:
  - port: prom-exporter

# Optimized resources
resources:
  limits:
    cpu: "1000m"
    memory: "1Gi"
  requests:
    cpu: "100m"
    memory: "128Mi"

# We expose the Prometheus exporter metrics port
ports:
  prom-exporter:
    enabled: true
    containerPort: 8889
    servicePort: 8889
    protocol: TCP

config:
  receivers:
    # OTLP receiver for metrics, traces and logs
    otlp:
      protocols:
        grpc:
          endpoint: 0.0.0.0:4317
        http:
          endpoint: 0.0.0.0:4318

  processors:
    memory_limiter:
      check_interval: 5s
      limit_percentage: 80
      spike_limit_percentage: 25
    batch: {}
    # Add attributes processor for better log labeling
    attributes:
      actions:
        - key: loki.attribute.labels
          action: insert
          value: service.name, service.namespace

  exporters:
    # Metrics -> Prometheus
    prometheus:
      endpoint: "0.0.0.0:8889"
    
    # Traces -> Tempo
    otlp/tempo:
      endpoint: "tempo.observability.svc.cluster.local:4317"
      tls:
        insecure: true
    
    # Logs -> Loki
    loki:
      endpoint: "http://loki.observability.svc.cluster.local:3100/loki/api/v1/push"
    
    # Optional debug exporter
    debug:
      verbosity: detailed

  service:
    pipelines:
      # Metrics pipeline
      metrics:
        receivers: [otlp]
        processors: [memory_limiter, batch]
        exporters: [prometheus]
      
      # Traces pipeline
      traces:
        receivers: [otlp]
        processors: [memory_limiter, batch]
        exporters: [otlp/tempo, debug]
      
      # Logs pipeline
      logs:
        receivers: [otlp]
        processors: [memory_limiter, batch, attributes]
        exporters: [loki, debug]
```

Install the Collector:
```bash
helm repo add open-telemetry https://open-telemetry.github.io/opentelemetry-helm-charts

helm repo update

helm install otel open-telemetry/opentelemetry-collector -n observability -f otel-values.yaml
```

Verify the OpenTelemetry Collector is ready:
```bash
kubectl -n observability get pods

# Wait until you see something like:
# otel-opentelemetry-collector-xxxxxxxxx-xxxxx   1/1     Running   0          30s
```

> With this: your app will send **metrics, traces and logs** via **OTLP** to the Collector. The Collector:
> - Exports **metrics** to **Prometheus** (exposes /metrics on port 8889, auto-discovered via ServiceMonitor)
> - Exports **traces** to **Tempo** (via OTLP gRPC on port 4317)
> - Exports **logs** to **Loki** (via HTTP API)
> 
> All telemetry signals are then available in **Grafana** for unified observability with full correlation between metrics, traces and logs.

---

## 8) Create the .NET API (Aspire‑ready) with OpenTelemetry

```bash
mkdir demo-aspire && cd demo-aspire
dotnet new webapi -n Demo.Api
cd Demo.Api

# Base OpenTelemetry packages (updated versions)
dotnet add package OpenTelemetry.Extensions.Hosting --version 1.9.0
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol --version 1.9.0
dotnet add package OpenTelemetry.Instrumentation.AspNetCore --version 1.9.0
dotnet add package OpenTelemetry.Instrumentation.Http --version 1.9.0
dotnet add package OpenTelemetry.Instrumentation.Runtime --version 1.9.0

# Additional packages for logging with OTLP
dotnet add package Microsoft.Extensions.Logging --version 9.0.0

# Additional packages for Aspire-ready
dotnet add package Microsoft.Extensions.ServiceDiscovery --version 9.0.0
dotnet add package Microsoft.Extensions.Http.Resilience --version 9.0.0
```

`Program.cs` (with OTLP exporting metrics, traces and logs):

```csharp
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
```

> 📍 Important: The next `Dockerfile` is created in `demo-aspire/Demo.Api/` (the same directory as the `.csproj` file).

`Dockerfile`:
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy source code and build
COPY . .
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Create non-root user
RUN adduser --disabled-password --gecos "" --uid 1001 appuser

COPY --from=build /out .

# Set ownership
RUN chown -R appuser:appuser /app
USER appuser

ENV ASPNETCORE_URLS=http://+:8080
ENV OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-opentelemetry-collector.observability.svc.cluster.local:4317
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
ENTRYPOINT ["dotnet", "Demo.Api.dll"]
```

Build the image **inside Minikube** (remember the `docker-env` from step 3):
```powershell
# Configure docker-env (if you haven't done it)
& minikube -p demo docker-env | Invoke-Expression

# Build the image
docker build -t demo-api:0.1 .

# Verify the image is in Minikube
docker images | findstr demo-api
```

---

## 9) Deploy the app to Kubernetes

> ⚠️ Important: Always use the profile `-p demo` with all minikube commands.

Create `demo-api.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: demo-api
  namespace: apps
  labels:
    app: demo-api
spec:
  replicas: 1
  selector:
    matchLabels:
      app: demo-api
  template:
    metadata:
      labels:
        app: demo-api
    spec:
      containers:
        - name: demo-api
          image: demo-api:0.1
          imagePullPolicy: IfNotPresent
          ports:
            - containerPort: 8080
              name: http
          env:
            - name: OTEL_EXPORTER_OTLP_ENDPOINT
              value: "http://otel-opentelemetry-collector.observability.svc.cluster.local:4317"
            - name: ASPNETCORE_ENVIRONMENT
              value: "Development"
          livenessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 30
            periodSeconds: 10
          readinessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 5
          resources:
            requests:
              memory: "64Mi"
              cpu: "50m"
            limits:
              memory: "256Mi"
              cpu: "200m"
---
apiVersion: v1
kind: Service
metadata:
  name: demo-api
  namespace: apps
spec:
  selector:
    app: demo-api
  ports:
    - name: http
      port: 80
      targetPort: 8080
  type: NodePort
```

Create the namespace and apply the deployment:
```powershell
# Create the namespace
kubectl create namespace apps

# Apply the deployment
kubectl apply -f demo-api.yaml

# Expose the service
minikube -p demo service demo-api --url -n apps
```

Test the endpoint:
```powershell
# Use the URL returned by the previous command
curl <URL_returned_by_minikube>/ping

# Or check the pod directly
kubectl get pods -n apps
kubectl logs -n apps deployment/demo-api
```

---

## 🎬 Now we can start the visual demo!

👉 **[Go to the Demo →](./demo.md)**