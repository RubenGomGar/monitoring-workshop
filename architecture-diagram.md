# 🔍 Workshop: Local Observability - From Zero to Hero

## 💡 Why Observability?

In the modern world of microservices and distributed applications, **it's not enough to know that something broke**. We need to know:
- 🔍 **What exactly broke?**
- ⏱️ **When did the problem start?** 
- 🎯 **Where is the bottleneck?**
- 📊 **How does it affect users?**

Observability gives us **complete visibility** into the internal behavior of our applications through **metrics**, **logs**, and **traces**.

## 🚀 Hands-on: Complete Stack in 30 minutes

In this workshop we'll build **from scratch** a complete observability stack:

```
🏗️ Stack: Minikube + OpenTelemetry + Prometheus + Tempo + Loki + Grafana + .NET 9
🎯 Goal: Monitor a real API with metrics, traces and logs
⏱️ Time: ~30 minutes  
🌐 Scope: 100% local, no external dependencies
```

**What you'll learn:**
- ✅ Configure OpenTelemetry in .NET 9
- ✅ Deploy Prometheus + Tempo + Loki + Grafana with Helm
- ✅ Create OTLP → Collector → Prometheus/Tempo/Loki pipelines
- ✅ Visualize metrics, traces and logs from your application
- ✅ Configuration troubleshooting

---

## 🎬 Start Now!

👉 **[Go to Complete Workshop →](./setup.md)**

---

## 🏗️ Stack Architecture

### Visual Flow Diagram

```mermaid
%%{init: {'theme':'base', 'themeVariables': { 
  'primaryColor':'#f8fafc',
  'primaryTextColor':'#1e293b',
  'primaryBorderColor':'#cbd5e1',
  'lineColor':'#64748b',
  'secondaryColor':'#f1f5f9',
  'tertiaryColor':'#e2e8f0',
  'clusterBkg':'#f8fafc',
  'clusterBorder':'#cbd5e1',
  'edgeLabelBackground':'#ffffff',
  'fontFamily':'ui-sans-serif, system-ui, sans-serif'
}}}%%
graph TB
    %% Elegant modern styles - softer pastel colors
    classDef userStyle fill:#dcfce7,stroke:#86efac,stroke-width:2px,color:#166534,font-weight:bold
    classDef dockerStyle fill:#dbeafe,stroke:#60a5fa,stroke-width:2px,color:#1e3a8a,font-weight:bold
    classDef appStyle fill:#e9d5ff,stroke:#c084fc,stroke-width:2px,color:#6b21a8,font-weight:bold
    classDef otelStyle fill:#fed7aa,stroke:#fb923c,stroke-width:2px,color:#9a3412,font-weight:bold
    classDef tempoStyle fill:#cffafe,stroke:#22d3ee,stroke-width:2px,color:#164e63,font-weight:bold
    classDef lokiStyle fill:#fce7f3,stroke:#f472b6,stroke-width:2px,color:#9f1239,font-weight:bold
    classDef promStyle fill:#fee2e2,stroke:#f87171,stroke-width:2px,color:#991b1b,font-weight:bold
    classDef grafanaStyle fill:#ffedd5,stroke:#fdba74,stroke-width:2px,color:#9a3412,font-weight:bold
    classDef operatorStyle fill:#e0e7ff,stroke:#a5b4fc,stroke-width:2px,color:#3730a3,font-weight:bold
    
    %% User interaction layer
    User["<b>👤 USER</b><br/><br/>🌐 Web Browser<br/>💻 Local Access"]
    
    %% Infrastructure layer
    subgraph Local["<b>💻 LOCAL MACHINE</b>"]
        direction TB
        Docker["<b>🐳 DOCKER DESKTOP</b><br/><br/>Container Runtime"]
        
        subgraph K8s["<b>☸️ MINIKUBE CLUSTER</b>"]
            direction TB
            
            %% Application namespace
            subgraph AppNS["<b>📦 namespace: apps</b>"]
                direction TB
                API["<b>🚀 DEMO API</b><br/><br/>.NET 9 + OpenTelemetry<br/>📤 Port 8080"]
            end
            
            %% Observability namespace
            subgraph ObsNS["<b>📦 namespace: observability</b>"]
                direction TB
                Collector["<b>📡 OTEL COLLECTOR</b><br/><br/>Pipeline Engine<br/>📥 OTLP 4317/4318<br/>📤 Metrics 8889"]
                TempoSvc["<b>🔀 TEMPO</b><br/><br/>Traces Storage<br/>📥 OTLP 4317<br/>💾 Persistent"]
                LokiSvc["<b>📝 LOKI</b><br/><br/>Logs Storage<br/>📥 HTTP 3100<br/>💾 Persistent"]
            end
            
            %% Monitoring namespace
            subgraph MonNS["<b>📦 namespace: monitoring</b>"]
                direction TB
                PromOp["<b>⚙️ PROMETHEUS<br/>OPERATOR</b><br/><br/>Auto-Discovery"]
                Prom["<b>🔍 PROMETHEUS</b><br/><br/>Metrics Storage<br/>🌐 Port 9090<br/>💾 Time-Series DB"]
                Graf["<b>📈 GRAFANA</b><br/><br/>Unified Dashboards<br/>🌐 Port 3000<br/>👤 admin/password"]
                Alert["<b>🚨 ALERTMANAGER</b><br/><br/>Notifications"]
            end
        end
    end
    
    %% User connections - solid arrows for user interactions
    User -->|"<b>:3000</b><br/>Dashboards"| Graf
    User -->|"<b>:9090</b><br/>Metrics Query"| Prom
    User -->|"<b>curl</b><br/>Test API"| API
    
    %% Data flow - thick arrows for telemetry data
    API ==>|"<b>OTLP :4317</b><br/>Metrics/Traces/Logs"| Collector
    
    %% Collector distribution
    Collector ==>|"<b>HTTP :8889</b><br/>Prometheus Format"| Prom
    Collector ==>|"<b>OTLP :4317</b><br/>Traces"| TempoSvc
    Collector ==>|"<b>HTTP :3100</b><br/>Logs"| LokiSvc
    
    %% Backend to Grafana - dotted for queries
    Prom -.->|"<b>PromQL</b>"| Graf
    TempoSvc -.->|"<b>TraceQL</b>"| Graf
    LokiSvc -.->|"<b>LogQL</b>"| Graf
    
    %% Operator management
    PromOp -.->|"ServiceMonitor"| Collector
    PromOp -.->|"Config"| Prom
    PromOp -.->|"Rules"| Alert
    
    %% Infrastructure
    Docker -->|"Runtime"| K8s
    
    %% Apply styles
    class User userStyle
    class Docker dockerStyle
    class API appStyle
    class Collector otelStyle
    class TempoSvc tempoStyle
    class LokiSvc lokiStyle
    class Prom,Alert promStyle
    class Graf grafanaStyle
    class PromOp operatorStyle
```

## 📋 Components and Ports

| Component | Namespace | Port | Function |
|------------|-----------|---------|---------|
| 🚀 Demo API | `apps` | `8080` | .NET application with OpenTelemetry |
| 📡 OpenTelemetry Collector | `observability` | `4317` (OTLP gRPC), `4318` (OTLP HTTP), `8889` (metrics) | Receives OTLP → Exposes metrics/traces/logs |
| 🔍 Prometheus | `monitoring` | `9090` | Stores and queries metrics |
| 🔀 Grafana Tempo | `observability` | `4317` (OTLP), `3200` (HTTP) | Stores and queries traces |
| 📝 Grafana Loki | `observability` | `3100` (HTTP) | Stores and queries logs |
| 📈 Grafana | `monitoring` | `3000` | Dashboards and visualization |

## 🔄 Data Flow

```mermaid
sequenceDiagram
    participant User as 👤 User
    participant API as 🚀 Demo API
    participant OTEL as 📡 OTel Collector
    participant PROM as 🔍 Prometheus
    participant TEMPO as 🔀 Tempo
    participant LOKI as 📝 Loki
    participant GRAF as 📈 Grafana

    User->>API: GET /ping
    API-->>API: 📊 Generate OpenTelemetry signals
    API->>OTEL: OTLP gRPC (metrics + traces + logs)
    OTEL-->>OTEL: 🔄 Process and transform
    
    %% Metrics flow
    PROM->>OTEL: HTTP GET /metrics (scrape every 30s)
    OTEL->>PROM: 📈 Metrics in Prometheus format
    
    %% Traces flow
    OTEL->>TEMPO: OTLP gRPC (traces)
    TEMPO-->>TEMPO: 📍 Store traces
    
    %% Logs flow
    OTEL->>LOKI: HTTP POST /loki/api/v1/push (logs)
    LOKI-->>LOKI: 📋 Store logs
    
    %% Grafana queries
    User->>GRAF: 🌐 Access dashboard
    GRAF->>PROM: PromQL query
    PROM->>GRAF: 📊 Metrics data
    GRAF->>TEMPO: TraceQL query
    TEMPO->>GRAF: 📍 Traces data
    GRAF->>LOKI: LogQL query
    LOKI->>GRAF: 📋 Logs data
    GRAF->>User: 📈 Unified visualization
```

<div align="center">

## 🎯 Key Monitored Signals

```mermaid
%%{init: {'theme':'base', 'themeVariables': { 
  'primaryColor':'#f8fafc',
  'primaryTextColor':'#1e293b',
  'primaryBorderColor':'#cbd5e1',
  'lineColor':'#64748b',
  'secondaryColor':'#f1f5f9',
  'tertiaryColor':'#e2e8f0',
  'fontFamily':'ui-sans-serif, system-ui, sans-serif'
}}}%%
graph TB
    %% Elegant pastel styles
    classDef rootStyle fill:#fee2e2,stroke:#f87171,stroke-width:3px,color:#991b1b,font-weight:bold
    classDef metricsStyle fill:#dbeafe,stroke:#60a5fa,stroke-width:2px,color:#1e3a8a,font-weight:bold
    classDef tracesStyle fill:#cffafe,stroke:#22d3ee,stroke-width:2px,color:#164e63,font-weight:bold
    classDef logsStyle fill:#fce7f3,stroke:#f472b6,stroke-width:2px,color:#9f1239,font-weight:bold
    classDef itemStyle fill:#f1f5f9,stroke:#cbd5e1,stroke-width:1px,color:#475569
    
    Root["<b>📊 OBSERVABILITY SIGNALS</b>"]
    
    %% Column 1: Metrics
    Root --> Metrics["<b>📈 METRICS</b><br/>Performance & Health"]
    Metrics --> M1["HTTP routing<br/>attempts"]
    M1 --> M2["Request<br/>duration"]
    M2 --> M3["GC<br/>collections"]
    M3 --> M4["Active<br/>connections"]
    
    %% Column 2: Traces  
    Root --> Traces["<b>📍 TRACES</b><br/>Request Flow"]
    Traces --> T1["HTTP request<br/>spans"]
    T1 --> T2["Database<br/>queries"]
    T2 --> T3["External API<br/>calls"]
    T3 --> T4["End-to-end<br/>tracing"]
    
    %% Column 3: Logs
    Root --> Logs["<b>📋 LOGS</b><br/>Event Records"]
    Logs --> L1["Application<br/>events"]
    L1 --> L2["Error<br/>tracking"]
    L2 --> L3["Request<br/>logging"]
    L3 --> L4["Structured<br/>data"]
    
    %% Apply styles
    class Root rootStyle
    class Metrics metricsStyle
    class Traces tracesStyle
    class Logs logsStyle
    class M1,M2,M3,M4,T1,T2,T3,T4,L1,L2,L3,L4 itemStyle
```

</div>
</br>
</br>

> 🎉 **100% local and cloud-agnostic architecture!** 
> 
> Everything runs on your machine with Minikube + Docker, with no external dependencies or remote registries.
