# 🔍 Workshop: Observabilidad Local - De Zero a Hero

## 💡 ¿Por qué Observabilidad?

En el mundo moderno de microservicios y aplicaciones distribuidas, **no basta con saber que algo se rompió**. Necesitamos saber:
- 🔍 **¿Qué se rompió exactamente?**
- ⏱️ **¿Cuándo empezó el problema?** 
- 🎯 **¿Dónde está el cuello de botella?**
- 📊 **¿Cómo afecta a los usuarios?**

La observabilidad nos da **visibilidad total** del comportamiento interno de nuestras aplicaciones a través de **métricas**, **logs** y **trazas**.

## 🚀 Hands-on: Stack Completo en 30 minutos

En este workshop montaremos **desde cero** un stack completo de observabilidad:

```
🏗️ Stack: Minikube + OpenTelemetry + Prometheus + Grafana + .NET 9
🎯 Objetivo: Monitorear una API real con métricas en tiempo real
⏱️ Tiempo: ~30 minutos  
🌐 Alcance: 100% local, sin dependencias externas
```

**Lo que aprenderás:**
- ✅ Configurar OpenTelemetry en .NET 9
- ✅ Desplegar Prometheus + Grafana con Helm
- ✅ Crear pipelines de métricas OTLP → Collector → Prometheus
- ✅ Visualizar métricas reales de tu aplicación
- ✅ Troubleshooting de configuraciones

---

## 🎬 ¡Empezar Ahora!

👉 **[Ir al Workshop Completo →](./setup.md)**

---

## 🏗️ Arquitectura del Stack

### Diagrama de Flujo Visual

```mermaid
graph TB
    %% Estilo de los nodos
    classDef k8s fill:#326CE5,stroke:#ffffff,stroke-width:2px,color:#ffffff
    classDef app fill:#68217A,stroke:#ffffff,stroke-width:2px,color:#ffffff
    classDef otel fill:#F5A623,stroke:#ffffff,stroke-width:2px,color:#ffffff
    classDef prometheus fill:#E6522C,stroke:#ffffff,stroke-width:2px,color:#ffffff
    classDef grafana fill:#F46800,stroke:#ffffff,stroke-width:2px,color:#ffffff
    classDef docker fill:#0db7ed,stroke:#ffffff,stroke-width:2px,color:#ffffff
    classDef user fill:#28a745,stroke:#ffffff,stroke-width:2px,color:#ffffff

    %% Docker y Minikube
    subgraph "💻 Local Machine"
        Docker["🐳 Docker Desktop"]
        
        subgraph "☸️ Minikube Cluster (driver: docker)"
            subgraph "📦 Namespace: apps"
                DemoAPI["🚀 Demo API (.NET 9)\n📊 OpenTelemetry Instrumentation\n🔗 OTLP Endpoint"]
            end
            
            subgraph "📦 Namespace: observability"
                OTelCollector["📡 OpenTelemetry Collector\n🔄 OTLP → Prometheus\n📈 Metrics Export (8889)"]
            end
            
            subgraph "📦 Namespace: monitoring"
                Prometheus["🔍 Prometheus\n📊 Metrics Storage\n🎯 Auto-discovery"]
                Grafana["📈 Grafana\n📋 Dashboards\n👤 admin/password"]
                AlertManager["🚨 AlertManager"]
                PrometheusOperator["⚙️ Prometheus Operator\n🔍 ServiceMonitor Discovery"]
            end
        end
    end
    
    %% Usuario
    User["👤 Usuario\n🌐 Browser"]
    
    %% Conexiones principales
    User -->|":3000 📈 Dashboard"| Grafana
    User -->|":9090 🔍 Queries"| Prometheus
    User -->|"curl /ping 🏃"| DemoAPI
    
    %% Flujo de datos
    DemoAPI -->|"OTLP gRPC :4317\n📊 Metrics + Traces"| OTelCollector
    OTelCollector -->|"HTTP :8889\n📈 /metrics endpoint"| Prometheus
    Prometheus -->|"PromQL Queries\n📊 Data Source"| Grafana
    
    %% ServiceMonitor
    PrometheusOperator -->|"🎯 Auto-discovery\nServiceMonitor"| OTelCollector
    PrometheusOperator -->|"⚙️ Config Management"| Prometheus
    
    %% Docker relationship
    Docker -->|"🏗️ Container Runtime"| Minikube
    
    %% Aplicar estilos
    class Docker,Minikube docker
    class DemoAPI app
    class OTelCollector otel
    class Prometheus,PrometheusOperator,AlertManager prometheus
    class Grafana grafana
    class User user
```

## 📋 Componentes y Puertos

| Componente | Namespace | Puerto | Función |
|------------|-----------|---------|---------|
| 🚀 Demo API | `apps` | `8080` | Aplicación .NET con OpenTelemetry |
| 📡 OpenTelemetry Collector | `observability` | `4317` (OTLP), `8889` (metrics) | Recibe OTLP → Expone métricas |
| 🔍 Prometheus | `monitoring` | `9090` | Almacena y consulta métricas |
| 📈 Grafana | `monitoring` | `3000` | Dashboards y visualización |

## 🔄 Flujo de Datos

```mermaid
sequenceDiagram
    participant User as 👤 Usuario
    participant API as 🚀 Demo API
    participant OTEL as 📡 OTel Collector
    participant PROM as 🔍 Prometheus
    participant GRAF as 📈 Grafana

    User->>API: GET /ping
    API-->>API: 📊 Genera métricas OpenTelemetry
    API->>OTEL: OTLP gRPC (métricas + trazas)
    OTEL-->>OTEL: 🔄 Procesa y transforma
    PROM->>OTEL: HTTP GET /metrics (scrape cada 30s)
    OTEL->>PROM: 📈 Métricas en formato Prometheus
    User->>GRAF: 🌐 Accede al dashboard
    GRAF->>PROM: PromQL query
    PROM->>GRAF: 📊 Datos de métricas
    GRAF->>User: 📈 Visualización
```

## 🎯 Métricas Clave Monitoreadas

```mermaid
mindmap
  root((📊 Métricas))
    🎯 Target Info
      deployment_environment="Development"
      telemetry_sdk_name="opentelemetry"
      exported_job="1.0.0/demo-api"
    🌐 HTTP Requests
      aspnetcore_routing_match_attempts_total
      http_server_request_duration_seconds
      http_client_active_requests
    🔧 Runtime .NET
      process_runtime_dotnet_gc_*
      process_runtime_dotnet_assemblies_count
      kestrel_active_connections
    ⚙️ Collector Status  
      up{{job="otel-opentelemetry-collector"}}
      scrape_duration_seconds
```

## 🚀 Stack Tecnológico

```mermaid
graph LR
    subgraph "🏗️ Infrastructure"
        A[🐳 Docker] --> B[☸️ Minikube]
        B --> C[🎛️ Helm Charts]
    end
    
    subgraph "📊 Observability Stack"
        D[📡 OpenTelemetry] --> E[🔍 Prometheus]
        E --> F[📈 Grafana]
        G[⚙️ Prometheus Operator] --> E
    end
    
    subgraph "🚀 Application"
        H[.NET 9] --> I[OpenTelemetry SDK]
        I --> J[OTLP Exporter]
    end
    
    C --> D
    C --> G
    C --> F
    J --> D

    %% Estilos
    classDef infra fill:#0db7ed,stroke:#ffffff,stroke-width:2px,color:#ffffff
    classDef obs fill:#E6522C,stroke:#ffffff,stroke-width:2px,color:#ffffff
    classDef app fill:#68217A,stroke:#ffffff,stroke-width:2px,color:#ffffff
    
    class A,B,C infra
    class D,E,F,G obs
    class H,I,J app
```

---

> 🎉 **¡Arquitectura 100% local y cloud-agnostic!** 
> 
> Todo corre en tu máquina con Minikube + Docker, sin dependencias externas ni registries remotos.