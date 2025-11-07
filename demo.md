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

```powershell
# Get Grafana URL
minikube -p demo service -n monitoring kps-grafana --url

# Get password
kubectl -n monitoring get secret kps-grafana -o jsonpath="{.data.admin-password}" | ForEach-Object { [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($_)) }
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

## 9) (Optional) Add logs (Loki) and traces (Tempo)

To extend:
- Deploy **Loki** (`grafana/loki-stack`) and **Tempo** (`grafana/tempo` or `tempo-distributed`).
- In the Collector, add `loki` exporters (HTTP push `/loki/api/v1/push`) and `otlp` to Tempo (`:4317`).
- In Grafana, add **Loki** and **Tempo** datasources and explore logs/traces.

---

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

## Final result

```
Docker Host
└── Minikube (Docker driver)
    ├── kube-prometheus-stack (Prometheus + Grafana)
    ├── OpenTelemetry Collector (OTLP → /metrics)
    └── Demo.Api (.NET Aspire‑ready)
```

Minimal, reproducible stack ready for **cloud-agnostic** demos 🎯
