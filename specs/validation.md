# Validación local — PactoGG Backend

**Fecha:** 2026-11-04
**Alcance:** build, pruebas, SCA, Terraform, despliegue AWS de costo cero y smoke público. No acredita revisión humana ni DAST.

## Resultados

| Control | Resultado | Evidencia / límite |
|---|---|---|
| Build Release | PASS | 0 errores, 0 advertencias |
| UnitTest | PASS | 26 pruebas |
| IntegrationTest | PASS | 11 pruebas |
| SecurityTest | PASS | 6 pruebas |
| ContractTest | PASS | 3 pruebas |
| DatabaseTest | PASS local / 1 omitida | Testcontainers omitido: integración WSL de Docker no habilitada |
| MutationTest | PASS | 1 prueba de flujo |
| PerformanceTest | PASS | 1 prueba de flujo |
| Total | 49 verdes, 1 omitida | Suites locales |
| SCA | PASS | Sin paquetes vulnerables en 8 proyectos |
| Terraform fmt/validate | PASS | `scripts/deploy-first-time.sh` 2026-11-04 |
| YAML/shell/JSON | PASS | Workflow, scripts y Stryker |
| Expedientes SDD | PASS | 11 expedientes con `spec`, `plan`, `tasks` y checklist |
| Enlaces Markdown | PASS | 69 documentos, 0 enlaces relativos rotos |
| Docker build/push | PASS | Imagen publicada en ECR `pactogg-api:latest` (scan on push activo) |
| Credenciales AWS | PASS | Perfil `pactogg`; SCP de "Sign up for AWS (new)" resuelta activando características avanzadas |
| Terraform plan/apply | PASS | 2026-11-04: VPC, 2 EC2 t3.micro (us-east-1a/b), 2 Elastic IP, S3 + CloudFront, CloudWatch, SSM, IAM/OIDC (F2-D19) |
| Atlas real | PASS | Registro, login y creación de grupo persistidos en Atlas desde AWS |
| Smoke público | PASS | `https://52-3-200-211.sslip.io/health` → 200 `Healthy` (TLS Let's Encrypt vía Caddy) |
| E2E público | PASS | USR-01 201, AUTH-01 200, GRP-01 201, TOR-02 200, AUTH-04 204 contra la URL pública |
| Cambios F2-D20/F2-D21 | PENDIENTE | `voting` con la primera evidencia y auto-reto 422: código y pruebas escritos; falta `dotnet test` y redespliegue |

## Configuración local de MongoDB

La URI de Atlas se cargó desde `/mnt/c/Users/david/Downloads/atlas-credentials.env` en `Pactogg.Api/appsettings.Development.local.json`, ignorado por Git. No se copió a `appsettings.json`, OpenAPI, Terraform ni documentación. La conexión TLS y la verificación de índices funcionan desde este entorno; la API arranca y `/health` responde 200. El flujo E2E contra Atlas desde AWS se verificó el 2026-11-04.

## Comandos ejecutados

```bash
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
dotnet build Pactogg.sln -c Release
dotnet test UnitTest/UnitTest.csproj -c Release --no-build
dotnet test IntegrationTest/IntegrationTest.csproj -c Release --no-build
dotnet test SecurityTest/SecurityTest.csproj -c Release --no-build
dotnet test ContractTest/ContractTest.csproj -c Release --no-build
dotnet test DatabaseTest/DatabaseTest.csproj -c Release --no-build
dotnet test MutationTest/MutationTest.csproj -c Release --no-build
dotnet test PerformanceTest/PerformanceTest.csproj -c Release --no-build
dotnet list Pactogg.sln package --vulnerable --include-transitive
docker build -t pactogg-api:local .
terraform fmt -check -recursive
terraform validate
```

## Límites

Los tests de integración usan fakes en memoria; no prueban MongoDB Atlas, Redis real, S3, Rekognition ni Comprehend. La conexión manual a Atlas e índices se verificó, pero no está cubierta por pruebas automatizadas. La cobertura y mutación no tienen umbral aprobado. Despliegue AWS verificado el 2026-11-04. Incidencias: descripción de reglas SG con apóstrofo (rechazada por AWS) dejó la instancia 1 sin egress en el primer arranque; se recrearon ambas instancias. Pendiente: variables de GitHub Actions (`AWS_ROLE_ARN`, `INSTANCE_IDS`, `PUBLIC_BASE_URL`).

