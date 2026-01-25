# 🔒 Security: Kubernetes and Docker Security Basics

Securing your Kubernetes cluster and containers is not optional—it's **essential**. In this hands-on section, we'll explore basic practical security measures you can implement right now.

## 💡 Why Security Matters?

In modern containerized environments, security breaches can happen at multiple levels:
- 🚨 **Vulnerable container images** with known CVEs
- 🔓 **Unrestricted network communication** between pods
- 👤 **Containers running as root** with excessive privileges
- 🎯 **Misconfigured RBAC** allowing unauthorized access

**🎯 What you'll learn:**
- ✅ Scan container images for vulnerabilities with Trivy
- ✅ Implement Network Policies to isolate pods
- ✅ Configure RBAC for least-privilege access
- ✅ Run containers as non-root users


## 1) 🔍 Image Vulnerability Scanning with Trivy

### 1.1) Install Trivy

You can download it and install it for any major distribution [here](https://github.com/aquasecurity/trivy/releases/). Or, follow these steps:

**On Linux/WSL:**
```bash
# Install Trivy
curl -sfL https://raw.githubusercontent.com/aquasecurity/trivy/main/contrib/install.sh | sudo sh -s -- -b /usr/local/bin v0.68.2

# Windows
choco install trivy

# Verify installation
trivy --version
```

**On Windows (PowerShell with Chocolatey):**
```powershell
choco install trivy -y
trivy --version
```

### 1.2) Scan Container Images

```bash
# Scan our demo-api image
trivy image demo-api:0.1 

# Filter scan by HIGH and CRITICAL vulnerabilities
trivy image --severity HIGH,CRITICAL demo-api:0.1 

# Export results to JSON
trivy image -f json -o results.json demo-api:0.1 
```

**What should you see?**

Trivy will output a table with:
- 📋 **CVE IDs**: Vulnerability identifiers (e.g., CVE-2024-1234)
- 🔴 **Severity**: CRITICAL, HIGH, MEDIUM, LOW
- 📦 **Package**: Affected library/package
- ✅ **Fixed Version**: Version that patches the vulnerability


## 2) 👤 Run Containers as Non-Root

### 2.1) Why Non-Root Matters

Running containers as root is dangerous:
- 🚨 **Container escape** = root on the host
- 💥 **File system access** without restrictions
- 🔓 **Privilege escalation** vulnerabilities

### 2.2) A Bad Dockerfile

**Dockerfile-bad (❌ insecure):**
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

# run as root for learning - DO NOT DO THIS IN ANY ENVIRONMENT!!
USER root 

ENV ASPNETCORE_URLS=http://+:8080
ENV OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-opentelemetry-collector.observability.svc.cluster.local:4317
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
ENTRYPOINT ["dotnet", "Demo.Api.dll"]
```

Name the previous file as Dockerfile.bad and build the image: 

```bash
docker build -f Dockerfile.bad -t demo-root:local .

# Load the image into Minikube's containerd runtime
minikube image load demo-root:local -p demo

# Verify the image is in Minikube
minikube image ls -p demo | findstr demo-root
```

### 2.3) Detect it with Trivy

Run this command to check for misconfigurations

```bash
trivy image --scanners misconfig --image-config-scanners misconfig  demo-root:local
```

### 2.4) In Kubernetes if we try to run the root container with restricted policies...

Update `demo-api.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: demo-api-root
  namespace: apps
  labels:
    app: demo-api-root
spec:
  replicas: 1
  selector:
    matchLabels:
      app: demo-api-root
  template:
    metadata:
      labels:
        app: demo-api-root
    spec:
      containers:
        - name: demo-api-root
          image: demo-root:local
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
          securityContext:
            allowPrivilegeEscalation: false
            runAsNonRoot: false
            runAsUser: 1001
---
apiVersion: v1
kind: Service
metadata:
  name: demo-api-root
  namespace: apps
spec:
  selector:
    app: demo-api-root
  ports:
    - name: http
      port: 80
      targetPort: 8080
  type: NodePort
```

Apply and verify:
```bash
kubectl apply -f demo-api-root.yaml
```

What are you experiencing? Try entering the pod with a console.


## 3) 🛡️ Network Policies: Pod Firewall

### 3.1) Understanding the Problem

By default, **all pods in Kubernetes can communicate with each other**. This is a security risk!

**Let's demonstrate this:**

```bash
# Deploy a test pod with network tools
kubectl run infiltrated --image=nicolaka/netshoot -n monitoring -- sleep infinity

# Access the pod
kubectl exec -n monitoring -it infiltrated -- bash

# Inside the pod, scan the cluster
nmap -sn -v 10.244.0.0/24

# You'll see ALL pods responding!
```

### 3.2) Deploy a Network Policy

First test that you have open communications.

```bash
kubectl exec -n monitoring -it infiltrated -- bash
curl <IP-api-demo>:8080/health
# Should return healthy
```

Now create `deny-all-policy.yaml`:

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: deny-all
  namespace: apps
spec:
  podSelector:
    matchLabels:
      app: demo-api
  policyTypes:
  - Ingress
  - Egress
  ingress: []  # Deny all ingress
  egress: []   # Deny all egress
```

Apply the policy:
```bash
kubectl apply -f deny-all-policy.yaml

# Verify it exists
kubectl get networkpolicy -n apps
```

Test again if you are able to communicate:
```bash
kubectl exec -n monitoring -it infiltrated -- bash
curl <IP-api-demo>:8080/health
# Should not respond
```

---

## 4) 🎯 RBAC: Least Privilege Access

### 4.1) Create a Read-Only Service Account

Create `readonly-sa.yaml`:

```yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: pod-reader
  namespace: apps
---
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: pod-reader-role
  namespace: apps
rules:
- apiGroups: [""]
  resources: ["pods", "pods/log"]
  verbs: ["get", "list"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: pod-reader-binding
  namespace: apps
subjects:
- kind: ServiceAccount
  name: pod-reader
  namespace: apps
roleRef:
  kind: Role
  name: pod-reader-role
  apiGroup: rbac.authorization.k8s.io
```

Apply and test:
```bash
kubectl apply -f readonly-sa.yaml

# Test with the service account
kubectl auth can-i list pods --as=system:serviceaccount:apps:pod-reader -n apps
# Should return: yes

kubectl auth can-i delete pods --as=system:serviceaccount:apps:pod-reader -n apps
# Should return: no
```

# 5) Additional: Pentesting K8S

If you still have so time left, you can try this tool: [peirates](https://github.com/inguardians/peirates).

> ⚠️⚠️⚠️ Do NOT use these tools if you do not know what you are doing.

First, set a service account with plenty of permissions and a pentesting-pod:

```yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: pod-destroyer
  namespace: apps
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: pod-destroyer
rules:
- apiGroups: ["*"]
  resources: ["*"]
  verbs: ["*"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: pod-destroyer-binding
subjects:
- kind: ServiceAccount
  name: pod-destroyer
  namespace: apps
roleRef:
  kind: ClusterRole
  name: pod-destroyer
  apiGroup: rbac.authorization.k8s.io
```

Deploy this new service account and role:
```bash
kubectl apply -f destroyer-sa.yaml
```

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: k8s-pentest-pod
  namespace: apps
  labels:
    app: k8s-pentest-pod
spec:
  replicas: 1
  selector:
    matchLabels:
      app: k8s-pentest-pod
  template:
    metadata:
      labels:
        app: k8s-pentest-pod
    spec:
      serviceAccountName: pod-destroyer
      containers:
        - name: k8s-pentest-pod
          image: bustakube/alpine-peirates:v1.1.27d
          imagePullPolicy: IfNotPresent
```

```bash
kubectl apply -f k8s-pentest-pod.yaml
```

Now, access the pod:
```bash
# Open the tool
peirates

# Commands
list-pods
list-ns # Try to change to the monitoring namespace and list the pods again
list-sa

dump-pod-info
switch-ns 
```

# 6) 🚀 Pod Resize Without Restart

### 6.1) Build/tag the new resize image
```bash
# Build (or tag) the resize-friendly image from root directory
docker build -t demo-resize:0.1 -f demo-aspire/Demo.Api/Dockerfile demo-aspire/Demo.Api

# Alternative
docker tag demo-api:0.1 demo-resize:0.1

# Load the image
minikube image load demo-resize:0.1 -p demo
```

### 6.2) Deploy the resize-ready app
```bash
kubectl apply -f demo-api-resize.yaml
```

### 6.3) Patch the pod and observe the behaviour
```bash
# We recommend to keep running in parallel to observe if it restarts or not
kubectl get pods -n apps -l app=demo-resize -w

# Patch Memory - Should NOT restart
kubectl patch pod POD_NAME -n apps --subresource resize --patch '{"spec":{"containers":[{"name":"demo-resize", "resources":{"requests":{"memory":"100Mi"}, "limits":{"memory":"1Gi"}}}]}}'

# Path CPU - Should restart
kubectl patch pod POD_NAME -n apps --subresource resize --patch '{"spec":{"containers":[{"name":"demo-resize", "resources":{"requests":{"cpu":"100m"}, "limits":{"cpu":"400m"}}}]}}'

# Evaluate the changes in the pod
kubectl describe pod POD_NAME -n apps

```


## 📚 Additional Resources

### Documentation
- [K8S Security Checklist](https://kubernetes.io/docs/concepts/security/security-checklist/)
- [Kubernetes Network Policies](https://kubernetes.io/docs/concepts/services-networking/network-policies/)
- [RBAC Good Practices](https://kubernetes.io/docs/concepts/security/rbac-good-practices/)
- [Pod Security Standards](https://kubernetes.io/docs/concepts/security/pod-security-standards/)
- [OWASP Docker Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Docker_Security_Cheat_Sheet.html)
- [Kubernetes Pentesting Multi-cloud](https://cloud.hacktricks.wiki/en/pentesting-cloud/kubernetes-security/index.html)

### Tools
- [Trivy Official Docs](https://trivy.dev/dev/docs/scanner/vulnerability/)
---