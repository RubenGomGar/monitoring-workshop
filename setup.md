# 🚀 Workshop: Observabilidad mínima con **Minikube (driver Docker)** + **Prometheus/Grafana** + **OpenTelemetry** + **.NET (Aspire‑ready)**

Este workshop monta un entorno **100 % local** y **cloud‑agnostic** usando **Minikube dentro de Docker** (sin k3d ni Rancher), con:
- Kubernetes local (Minikube, driver Docker)
- Prometheus + Grafana (kube‑prometheus‑stack)
- OpenTelemetry Collector (OTLP → Prometheus)
- App .NET (Aspire‑ready) que exporta métricas/trazas por OTLP

> Objetivo: **la vía más simple posible**, sin registry externo ni configuraciones complejas de red.

---

## 1) Requisitos

Comprueba que tienes estas herramientas instaladas:

| Herramienta | Comando |
|---|---|
| Docker Desktop/Engine | `docker ps` |
| Minikube | `minikube version` |
| kubectl | `kubectl version --client` |
| Helm | `helm version` |
| .NET 9 SDK | `dotnet --version` |

Instalación rápida de Minikube (Windows con Chocolatey):
```bash
choco install minikube -y
```
(En Linux/Mac usa el método oficial de minikube).

---

## 2) Arranca Minikube dentro de Docker

```bash
minikube start -p demo --driver=docker
kubectl get nodes

minikube -p demo dashboard
```
Deberías ver 1 nodo `Ready`.

> **Tip (Windows/macOS):** asegúrate de que Docker está corriendo antes de iniciar Minikube.

---

## 3) Compilar imágenes directamente en el Docker de Minikube

Evita usar un registry. Construye dentro del daemon de Minikube:

```powershell
# En PowerShell (Windows):
& minikube -p demo docker-env | Invoke-Expression

# En Bash (Linux/Mac):
# eval $(minikube -p demo docker-env)

# Verifica que está configurado:
docker images
# a partir de aquí, cualquier 'docker build' se guarda en Minikube
```

---

## 4) Instala Prometheus + Grafana (kube‑prometheus‑stack)

```bash
kubectl create ns monitoring

helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update

helm install kps prometheus-community/kube-prometheus-stack -n monitoring   --set grafana.service.type=NodePort
```

**Espera a que todos los pods estén ready:**
```bash
kubectl -n monitoring get pods

# Espera hasta que todos muestren Ready (puede tardar 2-3 minutos)
# Deberías ver: alertmanager, grafana, kube-state-metrics, node-exporter, prometheus-operator, prometheus-server
```

Obtén la URL de Grafana:
```bash
minikube -p demo service -n monitoring kps-grafana --url
```

**Password de Grafana (usuario: `admin`):**

```bash
# Linux/macOS/Git Bash:
kubectl -n monitoring get secret kps-grafana -o jsonpath="{.data.admin-password}" | base64 -d; echo
```

```powershell
# PowerShell:
kubectl -n monitoring get secret kps-grafana -o jsonpath="{.data.admin-password}" | ForEach-Object { [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($_)) }
```

> **Nota:** kube‑prometheus‑stack ya configura Prometheus Operator, Alertmanager y Grafana con dashboards por defecto de Kubernetes.

---

## 5) Instala OpenTelemetry Collector (gateway mínimo)

Crea `otel-values.yaml` con este contenido:

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

# Recursos optimizados
resources:
  limits:
    cpu: "1000m"
    memory: "1Gi"
  requests:
    cpu: "100m"
    memory: "128Mi"

# Exportamos SOLO el puerto de métricas del exporter Prometheus
ports:
  prom-exporter:
    enabled: true
    containerPort: 8889
    servicePort: 8889
    protocol: TCP

config:
  receivers:
    # deja OTLP; el chart expondrá 4317/4318 automáticamente (no los declares a mano)
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

  exporters:
    prometheus:
      endpoint: "0.0.0.0:8889"
    # debug opcional para ver algo en logs
    # debug: {}

  service:
    pipelines:
      metrics:
        receivers: [otlp]
        processors: [memory_limiter, batch]
        exporters: [prometheus]
      # traces:
      #   receivers: [otlp]
      #   processors: [memory_limiter, batch]
      #   exporters: []   # añade Tempo/OTLP cuando quieras
      # logs:
      #   receivers: [otlp]
      #   processors: [memory_limiter, batch]
      #   exporters: []   # añade Loki/OTLP cuando quieras
```

Instala el Collector:
```bash
kubectl create ns observability
helm repo add open-telemetry https://open-telemetry.github.io/opentelemetry-helm-charts
helm repo update
helm install otel open-telemetry/opentelemetry-collector -n observability -f otel-values.yaml
```

**Verifica que el OpenTelemetry Collector está ready:**
```bash
kubectl -n observability get pods

# Espera hasta que veas algo como:
# otel-opentelemetry-collector-xxxxxxxxx-xxxxx   1/1     Running   0          30s
```

> Con esto: tu app enviará **métricas/trazas** por **OTLP** al Collector. El Collector **expone /metrics (8889)** y **Prometheus** lo scrapeará (ya auto‑descubierto por el Prometheus Operator si activas ServiceMonitor; en este mínimo, puedes scrappear el Service del Collector con un `PodMonitor/ServiceMonitor` opcional).

---

## 6) Crea la API .NET (Aspire‑ready) con OpenTelemetry

```bash
mkdir demo-aspire && cd demo-aspire
dotnet new webapi -n Demo.Api
cd Demo.Api

# Paquetes base de OpenTelemetry (versiones actualizadas)
dotnet add package OpenTelemetry.Extensions.Hosting --version 1.9.0
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol --version 1.9.0
dotnet add package OpenTelemetry.Instrumentation.AspNetCore --version 1.9.0
dotnet add package OpenTelemetry.Instrumentation.Http --version 1.9.0
dotnet add package OpenTelemetry.Instrumentation.Runtime --version 1.9.0

# Paquetes adicionales para Aspire-ready
dotnet add package Microsoft.Extensions.ServiceDiscovery --version 9.0.0
dotnet add package Microsoft.Extensions.Http.Resilience --version 9.0.0
```

`Program.cs` (mínimo con OTLP a Collector):

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
var serviceName = "demo-api";
var serviceVersion = "1.0.0";
var otlp = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
           ?? "http://otel-opentelemetry-collector.observability.svc.cluster.local:4317";

// ActivitySource para custom traces
var activitySource = new ActivitySource(serviceName);

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

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapGet("/ping", () => 
{
    using var activity = activitySource.StartActivity("ping-endpoint");
    activity?.SetTag("custom.endpoint", "ping");
    return Results.Ok(new { ok = true, at = DateTimeOffset.UtcNow, version = serviceVersion });
});

app.Run();
```

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

> **📍 Importante:** El `Dockerfile` se crea en el directorio `demo-aspire/Demo.Api/` (mismo directorio donde está el archivo `.csproj`).

Construye la imagen **dentro de Minikube** (recuerda el `docker-env` del paso 3):
```powershell
# Configurar docker-env (si no lo hiciste antes)
& minikube -p demo docker-env | Invoke-Expression

# Construir la imagen
docker build -t demo-api:0.1 .

# Verificar que la imagen está en Minikube
docker images | findstr demo-api
```

---

## 7) Despliega la app en Kubernetes

> **⚠️ Importante:** Recuerda usar siempre el perfil `-p demo` en todos los comandos de minikube.

Crea `demo-api.yaml`:

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

Crea el namespace y aplica el deployment:
```powershell
# Crear el namespace
kubectl create namespace apps

# Aplicar el deployment
kubectl apply -f demo-api.yaml

# Exponer el servicio
minikube -p demo service demo-api --url -n apps
```

Prueba el endpoint:
```powershell
# Usar la URL que devuelve el comando anterior
curl <URL_que_devuelve_minikube>/ping

# O verificar el pod directamente
kubectl get pods -n apps
kubectl logs -n apps deployment/demo-api
```

---


## 🎬 ¡Ahora podemos empezar la demo visual!

👉 **[Ir al a la Demo →](./demo.md)**