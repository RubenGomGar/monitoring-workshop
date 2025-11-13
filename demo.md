# 🎬 Visual Demo: Observability in Action

Time to see the magic! With the entire stack deployed, we can now do a **complete visual demo** that shows how observability works in real-time.

**🎯 What we're going to demonstrate:**
- ✅ **Complete pipeline working**: API → OpenTelemetry → Collector → Prometheus → Grafana
- ✅ **Real-time metrics**: Seeing how each request generates instant data
- ✅ **Interactive dashboards**: Querying and visualizing application behavior
- ✅ **Live troubleshooting**: Identifying bottlenecks and usage patterns

**🔄 Demo flow:**
1. **Verify Collector metrics** - Confirm data is arriving
2. **Generate real traffic** - Make requests to the API
3. **Query in Prometheus** - See metrics updating in real-time
4. **Visualize in Grafana** - Create dashboards and graphs

---

## 8) Verify that the OpenTelemetry Collector works

> **⚠️ Important note**: If the ServiceMonitor doesn't work immediately, you may need to apply the port patch as explained in the troubleshooting section.

### 8.1) First: Verify metrics directly from the Collector

```powershell
# Port-forward to access Collector metrics
kubectl port-forward -n observability svc/otel-opentelemetry-collector 8889:8889
```

Open your browser at: http://localhost:8889/metrics

**What should you see?**

**1. Most important metric - Your application target:**
```
target_info{deployment_environment="Development",instance="demo-api-6b8b488594-q59nl",job="1.0.0/demo-api",telemetry_sdk_language="dotnet",telemetry_sdk_name="opentelemetry",telemetry_sdk_version="1.9.0"} 1
```
This line confirms that OpenTelemetry is receiving metrics from your .NET API.

**2. Other important metrics:**
- `up{job="otel-opentelemetry-collector"}` - Collector status (should be 1)
- `aspnetcore_routing_match_attempts_total` - ASP.NET Core requests
- `http_client_active_requests` - Active HTTP requests
- `otelcol_receiver_accepted_spans_total` - Spans received by the Collector

💡 **Tip:** Use Ctrl+F to search for `target_info` - it's the key metric that confirms the connection.

### 8.2) Generate traffic in your application

```powershell
# Get your app URL
minikube -p demo service demo-api --url -n apps

# Generate some traffic (run several times)
curl <URL>/ping
curl <URL>/ping
curl <URL>/ping
```

**Refresh http://localhost:8889/metrics** - you should now see more metrics from your app.

### 8.3) Verify in Prometheus

```powershell
# Port-forward to Prometheus (new terminal)
kubectl port-forward -n monitoring svc/kps-kube-prometheus-stack-prometheus 9090:9090
```

Open: http://localhost:9090

**Step by step in the Prometheus UI:**

#### Query 1: Is the Collector active?
- In the search box, type: `up{job="otel-opentelemetry-collector"}`
- Click **Execute**
- **You should see**: `up{...} 1` (means it's UP/active)
- **If you see 0 or nothing appears**: The ServiceMonitor is not working

#### Query 2: ✅ Are metrics arriving from your .NET application?
- Type: `aspnetcore_routing_match_attempts_total`
- Click **Execute**
- **You should see**: Metrics with `exported_job="1.0.0/demo-api"` and routes like `/health`, `/ping`
- **If nothing appears**: Your app is not sending metrics

#### Query 3: 🎯 Does the complete App → Collector connection work?
- Type: `target_info{telemetry_sdk_language="dotnet"}`
- Click **Execute**
- **You should see**: The metric with `deployment_environment="Development"` and `telemetry_sdk_name="opentelemetry"`
- **This is the MOST important metric** - confirms that OpenTelemetry is collecting data from your API

#### Query 4: 📊 Are HTTP requests being recorded?
- Type: `http_server_request_duration_seconds_count`
- Click **Execute**
- **You should see**: HTTP request counters with routes like `/health` and `/ping`
- **Includes detailed metrics**: Response codes, duration, etc.

#### Query 5: See all available metrics
- Type: `{job="otel-opentelemetry-collector"}`
- Click **Execute**
- **You should see**: A list of ALL metrics coming from the Collector (including .NET runtime metrics like `process_runtime_dotnet_*`)

> **💡 Tip**: If any query returns no results, go to **Status → Targets** in Prometheus to see if the Collector appears as a target.

#### 🔧 Query troubleshooting:

**If `up{job="otel-opentelemetry-collector"}` doesn't work:**
```powershell
# Verify that the ServiceMonitor exists and has the correct label
kubectl get servicemonitor -n observability --show-labels

# Verify that the ServiceMonitor points to the correct port
kubectl get servicemonitor -n observability otel-opentelemetry-collector -o yaml

# If necessary, patch the ServiceMonitor to use the correct port
kubectl patch servicemonitor -n observability otel-opentelemetry-collector --type='json' -p='[{"op": "replace", "path": "/spec/endpoints/0/port", "value": "prom-exporter"}]'
```

**If you don't see metrics from your app:**
```powershell
# Check Collector logs
kubectl logs -n observability deployment/otel-opentelemetry-collector

# Check your app logs
kubectl logs -n apps deployment/demo-api

# Check connectivity
kubectl exec -n apps deployment/demo-api -- nslookup otel-opentelemetry-collector.observability.svc.cluster.local
```

**If Prometheus doesn't find the Collector as a target:**
- Go to http://localhost:9090/targets
- Search for `otel-opentelemetry-collector`
- If it appears in red/down: network or configuration problem
- If it doesn't appear: problem with the ServiceMonitor

**Applied solution - Correct ServiceMonitor configuration:**
```powershell
# Common problem: ServiceMonitor looks for port 'metrics' but the Service uses 'prom-exporter'
# Solution: Patch the ServiceMonitor to use the correct port
kubectl patch servicemonitor -n observability otel-opentelemetry-collector --type='json' -p='[{"op": "replace", "path": "/spec/endpoints/0/port", "value": "prom-exporter"}]'

# Restart Prometheus if necessary
kubectl delete pod -n monitoring prometheus-kps-kube-prometheus-stack-prometheus-0

# Verify it works
kubectl port-forward -n monitoring svc/kps-kube-prometheus-stack-prometheus 9090:9090
# Then test: up{job="otel-opentelemetry-collector"}
```

### 8.4) Finally: View in Grafana

Powershell:
```powershell
# Get Grafana URL
minikube -p demo service -n monitoring kps-grafana --url

# Get password
kubectl -n monitoring get secret kps-grafana -o jsonpath="{.data.admin-password}" | ForEach-Object { [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($_)) }
```

Linux/macOS/Git Bash:
```bash
kubectl -n monitoring get secret kps-grafana -o jsonpath="{.data.admin-password}" | base64 -d; echo
```

**In Grafana:**
1. Go to **Explore**
2. Select **Prometheus** as data source
3. Try these queries:

```promql
# Your application traffic (.NET metrics)
rate(aspnetcore_routing_match_attempts_total[5m])

# Collector status
up{job="otel-opentelemetry-collector"}

# Requests by specific endpoint
aspnetcore_routing_match_attempts_total{http_route="/ping"}
```

### 8.5) Create a simple dashboard

In Grafana, create a **New Dashboard** with these panels:

1. **Panel 1 - Collector Health**:
   - Query: `up{job="otel-opentelemetry-collector"}`
   - Visualization: Stat

2. **Panel 2 - Request Rate by Endpoint**:
   - Query: `rate(aspnetcore_routing_match_attempts_total[5m])`
   - Visualization: Time series

3. **Panel 3 - Requests to /ping endpoint**:
   - Query: `aspnetcore_routing_match_attempts_total{http_route="/ping"}`
   - Visualization: Stat

---

## 9) Configure Grafana Data Sources for Tempo and Loki

Now that Tempo and Loki are deployed, let's configure them in Grafana to enable complete observability.

### 9.1) Access Grafana

```powershell
# Get Grafana URL
minikube -p demo service -n monitoring kps-grafana --url

# Get admin password
kubectl -n monitoring get secret kps-grafana -o jsonpath="{.data.admin-password}" | ForEach-Object { [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($_)) }
```

Login with:
- **User**: `admin`
- **Password**: (from the command above)

### 9.2) Add Loki Data Source

1. Click **Add data source** again
2. Search for **Loki**
3. Configure:
   - **Name**: `Loki`
   - **URL**: `http://loki.observability:3100`
4. Click **Save & test**

You should see: ✅ **Data source is working**

### 9.3) Add Tempo Data Source

1. Go to **Configuration → Data Sources** (or **Connections → Data Sources**)
2. Click **Add data source**
3. Search for **Tempo**
4. Configure:
   - **Name**: `Tempo`
   - **URL**: `http://tempo.observability:3200`
   - **Trace to logs**
     - **Datasource**: `Loki`
     - **Span start time shift**: `-5m`
     - **Span end time shift**: `5m`
     - **Tags**: service.name -> service_name And service.namespace -> service_namespace
   - **Trace to metrics**
     - **Datasource**: `prometheus`
     - **Span start time shift**: `-2m`
     - **Span end time shift**: `2m`
     - **Link Label**: Request Rate -> rate(aspnetcore_routing_match_attempts_total{service_name="${__span.tags["service.name"]}"}[5m])
5. Click **Save & test**

You should see: ✅ **Data source is working**

## 17) Final result

### 10.1) Generate traffic with traces

```powershell
# Get your app URL
minikube -p demo service demo-api --url -n apps

# Generate multiple requests to create traces
for ($i=1; $i -le 10; $i++) {
    curl <URL>/ping
    Start-Sleep -Milliseconds 500
}
```

### 10.2) Query traces in Grafana

1. In Grafana, go to **Explore**
2. Select **Tempo** as data source
3. Click on **Search** tab
4. You should see recent traces from your application
5. Click on any trace to see the detailed span view

**What you should see:**
- **Service**: `demo-api`
- **Operation**: `ping-endpoint` or `GET /ping`
- **Spans**: HTTP request spans with timing information
- **Tags**: `http.route=/ping`, `deployment.environment=Development`, etc.

### 10.3) Advanced trace search

Try these searches:
- **By service**: `{ service.name="demo-api" }`
- **By operation**: `{ name="GET /ping" }`
- **By duration**: Filter traces taking more than 100ms
- **By tag**: `{ http.route="/ping" }`

---

## 11) Verify Logs in Loki

### 11.1) Generate logs

Your application is already sending logs via OpenTelemetry. Generate some activity:

```powershell
# Generate requests (this will create INFO logs)
for ($i=1; $i -le 5; $i++) {
    curl <URL>/ping
    Start-Sleep -Seconds 1
}
```

> **📋 Understanding Loki Labels**: 
> The logs in Loki have these labels (you can see them in the Grafana UI when expanding a log entry):
> - `job`: `1.0.0/demo-api` (combination of service namespace + name)
> - `service_name`: `1.0.0/demo-api`
> - `exporter`: `OTLP`
> - `level`: `INFO`, `WARNING`, `ERROR`, etc.
> - `instance`: Pod name (e.g., `demo-api-7585b9878d-196vj`)
>
> Use these labels in your LogQL queries!

### 11.2) Query logs in Grafana

1. In Grafana, go to **Explore**
2. Select **Loki** as data source
3. Try these LogQL queries:

```logql
# All logs from demo-api (use the correct job label)
{job="1.0.0/demo-api"}

# Only INFO level logs
{job="1.0.0/demo-api", level="INFO"}

# Logs from specific endpoint
{job="1.0.0/demo-api"} |= "Ping endpoint called"

# Search by exporter
{exporter="OTLP"}

# Using regex for service name
{service_name=~".*demo-api.*"}
```

> **⚠️ Important**: The service labels are `job="1.0.0/demo-api"` and `service_name="1.0.0/demo-api"` because OpenTelemetry combines the service namespace (version) with the service name.

**What you should see:**
- Log messages from your .NET application
- Timestamps and log levels
- Service name and other resource attributes
- Structured log data including trace IDs

### 11.3) Log-to-Trace correlation

1. In the log results, expand a log entry by clicking on it
2. Look for the **trace_id** field in the structured data
3. You should see the trace ID associated with each log entry
4. Copy the trace ID
5. Go to **Explore** → **Tempo** → **Search** → **TraceID** and paste it
6. This shows the complete context: what happened (log) and how long it took (trace)

> **💡 Tip**: In the log details, you can see fields like:
> - `traceid`: The unique identifier for the trace
> - `spanid`: The specific span within the trace
> - `severity`: Log level (Information, Warning, Error)
> - `body`: The actual log message

---

## 12) Create a Unified Observability Dashboard

Let's create a dashboard that shows metrics, logs, and traces together.

### 12.1) Create new dashboard

1. In Grafana, go to **Dashboards → New → New Dashboard**
2. Click **Add visualization**

### 12.2) Panel 1 - Request Rate (Metrics)

- **Data source**: Prometheus
- **Query**: `rate(aspnetcore_routing_match_attempts_total{http_route="/ping"}[5m])`
- **Title**: "Requests per Second - /ping"
- **Visualization**: Time series
- Click **Apply**

### 12.3) Panel 2 - Request Duration (Traces)

- Click **Add → Visualization**
- **Data source**: Prometheus
- **Query**: `rate(http_server_request_duration_seconds_sum[5m]) / rate(http_server_request_duration_seconds_count[5m])`
- **Legend**: `Average Duration`
- **Title**: "Request Duration - Average"
- **Visualization**: Time series

### 12.4) Panel 3 - Recent Logs (Logs)

- Click **Add → Visualization**
- **Data source**: Loki
- **Query**: `{job="1.0.0/demo-api"} |= "Ping"`
- **Title**: "Recent Application Logs"
- **Visualization**: Logs
- **Options**: Show time, Show labels
- Click **Apply**

### 12.5) Panel 4 - Recent Traces (Traces)

- Click **Add → Visualization**
- **Data source**: Tempo
- In the query builder:
  - **Query type**: **Search**
  - **Service name**: Leave empty or type `demo-api`
  - **Span name**: Leave empty
  - **Min duration**: Leave empty
  - **Max duration**: Leave empty
  - **Limit**: 20
- **Title**: "Recent Traces"
- **Visualization**: **Table** (for a clean list view)
- **Transform data** (optional):
  - Click **Transform** tab
  - Add transformation: **Organize fields**
  - Show only: Service Name, Span Name, Duration, Start Time
- Click **Apply**

> **💡 Tip**: The Table visualization shows traces in a clear, readable format with columns for service, operation, duration, and timestamp. You can click on any trace row to see the full trace details.

### 12.6) Save the dashboard

1. Click the **Save dashboard** icon (💾)
2. **Name**: "Demo API - Full Observability"
3. Click **Save**

---

## 13) Demonstrate the Three Pillars of Observability

### 13.1) Metrics → Logs → Traces workflow

**Scenario**: Investigating application behavior

1. **Start with Metrics** (High-level view):
   - Go to your dashboard
   - Look at "Requests per Second" panel
   - Notice any spikes or anomalies

2. **Drill down to Logs** (Context):
   - Click on a time range in the metrics panel
   - Check the "Recent Application Logs" panel
   - See what was happening at that moment

3. **Deep dive with Traces** (Details):
   - Click on a log entry with a trace ID
   - Jump to the trace in Tempo
   - See exact timings, spans, and tags

### 13.2) Example queries for each pillar

**Metrics (Prometheus)**:
```promql
# Request rate
rate(aspnetcore_routing_match_attempts_total[5m])

# Error rate
rate(aspnetcore_routing_match_attempts_total{http_response_status_code=~"5.."}[5m])

# Memory usage
process_runtime_dotnet_gc_heap_size_bytes
```

**Logs (Loki)**:
```logql
# All logs from demo-api (correct label)
{job="1.0.0/demo-api"}

# Errors only
{job="1.0.0/demo-api"} |~ "(?i)error|exception|fail"

# Filter by log level
{job="1.0.0/demo-api", level="INFO"}

# Search in log message
{job="1.0.0/demo-api"} |= "Ping endpoint"
```

**Traces (Tempo)**:
- Search by service: `demo-api`
- Search by duration: `> 100ms`
- Search by tag: `http.status_code=200`

---

## 14) Advanced Correlation Examples

### 14.1) Find slow requests across all signals

**Step 1 - Metrics**: Find time range with high latency
```promql
histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket[5m])) > 0.1
```

**Step 2 - Traces**: Search for slow traces in that time range
- Go to Tempo
- Filter by service: `demo-api`
- Filter by duration: `> 100ms`
- Look at the trace details

**Step 3 - Logs**: Check logs for those traces
```logql
{job="1.0.0/demo-api"} | json | traceid="<trace_id_from_tempo>"
```

> **💡 Note**: The trace ID field in Loki logs is `traceid` (lowercase, no underscore)

### 14.2) Investigate errors end-to-end

**Step 1 - Metrics**: Detect error rate
```promql
rate(aspnetcore_routing_match_attempts_total{http_response_status_code=~"5.."}[5m])
```

**Step 2 - Logs**: Find error messages
```logql
{job="1.0.0/demo-api"} |~ "(?i)error|exception"
```

**Step 3 - Traces**: See what caused the error
- Click on trace ID from logs
- Navigate to Tempo
- Analyze spans and tags

---

## 15) Test the Complete Stack

Run this comprehensive test:

```powershell
# Generate varied traffic
$url = "<YOUR_MINIKUBE_SERVICE_URL>"

# Normal requests
1..20 | ForEach-Object {
    curl "$url/ping"
    Start-Sleep -Milliseconds 200
}

# Check all data sources in Grafana:
# 1. Prometheus: rate(aspnetcore_routing_match_attempts_total[1m])
# 2. Loki: {job="1.0.0/demo-api"} |= "Ping endpoint called"
# 3. Tempo: Search for service "demo-api"
```

**Verify in Grafana**:
1. ✅ Metrics showing in Prometheus
2. ✅ Logs appearing in Loki
3. ✅ Traces visible in Tempo
4. ✅ Correlation links working (click trace ID in logs → opens in Tempo)

---

## 16) Quick Troubleshooting for Tempo & Loki

## 10) Quick troubleshooting

### Minikube issues:
- **Error "Profile not found"**: Always use `-p demo` in minikube commands
  ```powershell
  # ❌ Incorrect: minikube service demo-api --url -n apps
  # ✅ Correct:   minikube -p demo service demo-api --url -n apps
  ```
- **`kubectl get nodes` doesn't respond**: 
  ```powershell
  minikube status -p demo
  # If necessary: minikube delete -p demo && minikube start -p demo --driver=docker
  ```

### Image issues:
- **`ImagePullBackOff`**: 
  ```powershell
  # Configure docker-env BEFORE building
  & minikube -p demo docker-env | Invoke-Expression
  docker build -t demo-api:0.1 .
  # Verify: docker images | findstr demo-api
  ```

### Service issues:
- **Grafana won't open**: 
  ```powershell
  minikube service -n monitoring kps-grafana --url -p demo
  ```
- **Grafana password**:
  ```powershell
  kubectl -n monitoring get secret kps-grafana -o jsonpath="{.data.admin-password}" | ForEach-Object { [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($_)) }
  ```

### OpenTelemetry issues:
- **No app metrics**: 
  ```powershell
  # Check the Collector
  kubectl -n observability get pods
  kubectl -n observability logs deploy/otel-opentelemetry-collector
  
  # Check the app
  kubectl logs deploy/demo-api -n apps
  
  # Check connectivity
  kubectl -n observability get svc
  ```

### Useful diagnostic commands:
```powershell
# General cluster status
kubectl get all --all-namespaces

# Cluster events
kubectl get events --sort-by=.metadata.creationTimestamp

# Verify Collector metrics
kubectl port-forward -n observability svc/otel-opentelemetry-collector 8889:8889
# Then visit: http://localhost:8889/metrics
```

---

## 17) Final result

```
Docker Host
└── Minikube (Docker driver)
    ├── Namespace: monitoring
    │   ├── Prometheus (metrics storage & queries)
    │   └── Grafana (unified visualization)
    │
    ├── Namespace: observability
    │   ├── OpenTelemetry Collector (telemetry gateway)
    │   │   ├── Receives: OTLP (metrics, traces, logs)
    │   │   ├── Exports metrics → Prometheus (:8889)
    │   │   ├── Exports traces → Tempo (:4317)
    │   │   └── Exports logs → Loki (HTTP API)
    │   ├── Tempo (distributed tracing backend)
    │   └── Loki (log aggregation system)
    │
    └── Namespace: apps
        └── Demo.Api (.NET 9 with OpenTelemetry)
            ├── Sends metrics via OTLP
            ├── Sends traces via OTLP
            └── Sends logs via OTLP
```

**Complete observability stack with:**
- ✅ **Metrics** (Prometheus) - Performance & health monitoring
- ✅ **Traces** (Tempo) - Distributed tracing & latency analysis
- ✅ **Logs** (Loki) - Application logs & debugging
- ✅ **Unified view** (Grafana) - Single pane of glass
- ✅ **Full correlation** - Jump between metrics, logs, and traces
- ✅ **100% Open Source** - No vendor lock-in
- ✅ **Cloud-agnostic** - Runs anywhere Kubernetes runs

Minimal, reproducible, production-ready observability stack 🎯
