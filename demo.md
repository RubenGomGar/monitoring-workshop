# 🎬 Demo Visual: Observabilidad en Acción

¡Es hora de ver la magia! Con todo el stack montado, ahora podemos hacer una **demo visual completa** que muestra cómo la observabilidad funciona en tiempo real. 

**🎯 Lo que vamos a demostrar:**
- ✅ **Pipeline completo funcionando**: API → OpenTelemetry → Collector → Prometheus → Grafana
- ✅ **Métricas en tiempo real**: Viendo cómo cada request genera datos instantáneos
- ✅ **Dashboards interactivos**: Consultando y visualizando el comportamiento de la aplicación
- ✅ **Troubleshooting en vivo**: Identificando cuellos de botella y patrones de uso

**🔄 Flujo de la demo:**
1. **Verificar métricas del Collector** - Confirmar que llegan datos
2. **Generar tráfico real** - Hacer requests a la API 
3. **Consultar en Prometheus** - Ver métricas actualizándose en tiempo real
4. **Visualizar en Grafana** - Crear dashboards y gráficos

---

## 8) Verificar que el OpenTelemetry Collector funciona

> **⚠️ Nota importante**: Si el ServiceMonitor no funciona inmediatamente, puede que necesites aplicar el patch del puerto como se explica en la sección de troubleshooting.

### 8.1) Primero: Verificar métricas directamente del Collector

```powershell
# Port-forward para acceder a las métricas del Collector
kubectl port-forward -n observability svc/otel-opentelemetry-collector 8889:8889
```

Abre tu navegador en: http://localhost:8889/metrics

**¿Qué deberías ver?**

**1. Métrica más importante - Target de tu aplicación:**
```
target_info{deployment_environment="Development",instance="demo-api-6b8b488594-q59nl",job="1.0.0/demo-api",telemetry_sdk_language="dotnet",telemetry_sdk_name="opentelemetry",telemetry_sdk_version="1.9.0"} 1
```
Esta línea confirma que OpenTelemetry está recibiendo métricas de tu API .NET.

**2. Otras métricas importantes:**
- `up{job="otel-opentelemetry-collector"}` - Estado del Collector (debe ser 1)
- `aspnetcore_routing_match_attempts_total` - Requests de ASP.NET Core
- `http_client_active_requests` - Requests HTTP activos
- `otelcol_receiver_accepted_spans_total` - Spans recibidos por el Collector

💡 **Tip:** Usa Ctrl+F para buscar `target_info` - es la métrica clave que confirma la conexión.

### 8.2) Generar tráfico en tu aplicación

```powershell
# Obtén la URL de tu app
minikube -p demo service demo-api --url -n apps

# Genera algo de tráfico (ejecuta varias veces)
curl <URL>/ping
curl <URL>/ping
curl <URL>/ping
```

**Refresca http://localhost:8889/metrics** - ahora deberías ver más métricas de tu app.

### 8.3) Verificar en Prometheus

```powershell
# Port-forward a Prometheus (nueva terminal)
kubectl port-forward -n monitoring svc/kps-kube-prometheus-stack-prometheus 9090:9090
```

Abre: http://localhost:9090

**Paso a paso en la UI de Prometheus:**

#### Query 1: ¿Está el Collector activo?
- En la caja de búsqueda, escribe: `up{job="otel-opentelemetry-collector"}`
- Click **Execute**
- **Deberías ver**: `up{...} 1` (significa que está UP/activo)
- **Si ves 0 o no aparece**: El ServiceMonitor no está funcionando

#### Query 2: ✅ ¿Llegan métricas de tu aplicación .NET?
- Escribe: `aspnetcore_routing_match_attempts_total`
- Click **Execute**
- **Deberías ver**: Métricas con `exported_job="1.0.0/demo-api"` y rutas como `/health`, `/ping`
- **Si no aparece**: Tu app no está enviando métricas

#### Query 3: 🎯 ¿Funciona la conexión completa App → Collector?
- Escribe: `target_info{telemetry_sdk_language="dotnet"}`
- Click **Execute**
- **Deberías ver**: La métrica con `deployment_environment="Development"` y `telemetry_sdk_name="opentelemetry"`
- **Esta es la métrica MÁS importante** - confirma que OpenTelemetry recoge datos de tu API

#### Query 4: 📊 ¿Se registran requests HTTP?
- Escribe: `http_server_request_duration_seconds_count`
- Click **Execute**
- **Deberías ver**: Contadores de requests HTTP con rutas como `/health` y `/ping`
- **Incluye métricas detalladas**: Códigos de respuesta, duración, etc.

#### Query 5: Ver todas las métricas disponibles
- Escribe: `{job="otel-opentelemetry-collector"}`
- Click **Execute**
- **Deberías ver**: Una lista de TODAS las métricas que vienen del Collector (incluyendo métricas del runtime de .NET como `process_runtime_dotnet_*`)

> **💡 Tip**: Si alguna query no devuelve resultados, ve a **Status → Targets** en Prometheus para ver si el Collector aparece como target.

#### 🔧 Troubleshooting de las consultas:

**Si `up{job="otel-opentelemetry-collector"}` no funciona:**
```powershell
# Verificar que el ServiceMonitor existe y tiene el label correcto
kubectl get servicemonitor -n observability --show-labels

# Verificar que el ServiceMonitor apunta al puerto correcto
kubectl get servicemonitor -n observability otel-opentelemetry-collector -o yaml

# Si necesario, patchear el ServiceMonitor para usar el puerto correcto
kubectl patch servicemonitor -n observability otel-opentelemetry-collector --type='json' -p='[{"op": "replace", "path": "/spec/endpoints/0/port", "value": "prom-exporter"}]'
```

**Si no ves métricas de tu app:**
```powershell
# Verificar logs del Collector
kubectl logs -n observability deployment/otel-opentelemetry-collector

# Verificar logs de tu app
kubectl logs -n apps deployment/demo-api

# Verificar conectividad
kubectl exec -n apps deployment/demo-api -- nslookup otel-opentelemetry-collector.observability.svc.cluster.local
```

**Si Prometheus no encuentra el Collector como target:**
- Ve a http://localhost:9090/targets
- Busca `otel-opentelemetry-collector`
- Si aparece en rojo/down: problema de red o configuración
- Si no aparece: problema con el ServiceMonitor

**Solución aplicada - Configuración correcta del ServiceMonitor:**
```powershell
# El problema común: ServiceMonitor busca puerto 'metrics' pero el Service usa 'prom-exporter'
# Solución: Patchear el ServiceMonitor para usar el puerto correcto
kubectl patch servicemonitor -n observability otel-opentelemetry-collector --type='json' -p='[{"op": "replace", "path": "/spec/endpoints/0/port", "value": "prom-exporter"}]'

# Reiniciar Prometheus si es necesario
kubectl delete pod -n monitoring prometheus-kps-kube-prometheus-stack-prometheus-0

# Verificar que funciona
kubectl port-forward -n monitoring svc/kps-kube-prometheus-stack-prometheus 9090:9090
# Luego probar: up{job="otel-opentelemetry-collector"}
```

### 8.4) Finalmente: Ver en Grafana

```powershell
# Obtener URL de Grafana
minikube -p demo service -n monitoring kps-grafana --url

# Obtener password
kubectl -n monitoring get secret kps-grafana -o jsonpath="{.data.admin-password}" | ForEach-Object { [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($_)) }
```

**En Grafana:**
1. Ve a **Explore**
2. Selecciona **Prometheus** como data source
3. Prueba estas consultas:

```promql
# Tráfico de tu aplicación (métricas .NET)
rate(aspnetcore_routing_match_attempts_total[5m])

# Estado del Collector
up{job="otel-opentelemetry-collector"}

# Requests por endpoint específico
aspnetcore_routing_match_attempts_total{http_route="/ping"}
```

### 8.5) Crear un dashboard simple

En Grafana, crea un **New Dashboard** con estos paneles:

1. **Panel 1 - Collector Health**:
   - Query: `up{job="otel-opentelemetry-collector"}`
   - Visualization: Stat

2. **Panel 2 - Request Rate por Endpoint**:
   - Query: `rate(aspnetcore_routing_match_attempts_total[5m])`
   - Visualization: Time series

3. **Panel 3 - Requests al endpoint /ping**:
   - Query: `aspnetcore_routing_match_attempts_total{http_route="/ping"}`
   - Visualization: Stat

---

## 9) (Opcional) Añadir logs (Loki) y trazas (Tempo)

Para extender:
- Despliega **Loki** (`grafana/loki-stack`) y **Tempo** (`grafana/tempo` o `tempo-distributed`).
- En el Collector, añade exporters `loki` (HTTP push `/loki/api/v1/push`) y `otlp` hacia Tempo (`:4317`).
- En Grafana, añade datasources **Loki** y **Tempo** y explora logs/trazas.

---

## 10) Troubleshooting rápido

### Problemas con Minikube:
- **Error "Profile not found"**: Siempre usa `-p demo` en comandos minikube
  ```powershell
  # ❌ Incorrecto: minikube service demo-api --url -n apps
  # ✅ Correcto:   minikube -p demo service demo-api --url -n apps
  ```
- **`kubectl get nodes` no responde**: 
  ```powershell
  minikube status -p demo
  # Si necesario: minikube delete -p demo && minikube start -p demo --driver=docker
  ```

### Problemas con imágenes:
- **`ImagePullBackOff`**: 
  ```powershell
  # Configurar docker-env ANTES de construir
  & minikube -p demo docker-env | Invoke-Expression
  docker build -t demo-api:0.1 .
  # Verificar: docker images | findstr demo-api
  ```

### Problemas con servicios:
- **Grafana no abre**: 
  ```powershell
  minikube service -n monitoring kps-grafana --url -p demo
  ```
- **Password de Grafana**:
  ```powershell
  kubectl -n monitoring get secret kps-grafana -o jsonpath="{.data.admin-password}" | ForEach-Object { [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($_)) }
  ```

### Problemas con OpenTelemetry:
- **No hay métricas de la app**: 
  ```powershell
  # Verificar el Collector
  kubectl -n observability get pods
  kubectl -n observability logs deploy/otel-opentelemetry-collector
  
  # Verificar la app
  kubectl logs deploy/demo-api -n apps
  
  # Verificar conectividad
  kubectl -n observability get svc
  ```

### Comandos útiles de diagnóstico:
```powershell
# Estado general del cluster
kubectl get all --all-namespaces

# Eventos del cluster
kubectl get events --sort-by=.metadata.creationTimestamp

# Verificar métricas del Collector
kubectl port-forward -n observability svc/otel-opentelemetry-collector 8889:8889
# Luego visita: http://localhost:8889/metrics
```

---

## Resultado final

```
Docker Host
└── Minikube (Docker driver)
    ├── kube-prometheus-stack (Prometheus + Grafana)
    ├── OpenTelemetry Collector (OTLP → /metrics)
    └── Demo.Api (.NET Aspire‑ready)
```

Stack mínimo, reproducible y listo para demos **cloud‑agnostic** 🎯
