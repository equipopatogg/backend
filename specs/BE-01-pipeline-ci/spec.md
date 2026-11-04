# BE-01 — Pipeline CI/CD, Docker y despliegue AWS

**Fecha:** 2026-11-04
**Estado:** Implementado localmente; runner/AWS pendientes.
**IDs:** BE-02, BE-21, BE-32

## 1. Contexto y objetivo

Automatizar build, pruebas, SAST, SCA, validación Terraform, construcción/escaneo/publicación de imagen y redespliegue en las 2 instancias EC2 por SSM Run Command con smoke test. La infraestructura es de costo cero (F2-D19): sin ALB, NAT, ASG ni ElastiCache. El pipeline no sustituye la aceptación externa.

## 2. Requisitos funcionales

- RF1: `build-test` restaura, compila Release y ejecuta los siete proyectos de prueba.
- RF2: `sast` ejecuta CodeQL para C#; `sca` ejecuta NuGet vulnerable y Trivy filesystem.
- RF3: `terraform` ejecuta `fmt -check`, `init -backend=false` y `validate`.
- RF4: `docker` construye, escanea con Trivy y publica en ECR; DockerHub es opcional con secretos.
- RF5: `deploy` asume rol OIDC y ejecuta `/opt/pactogg/redeploy.sh` en las instancias de `INSTANCE_IDS` vía SSM; espera `Success` en ambas.
- RF6: `smoke` consulta `PUBLIC_BASE_URL/health`.

## 3. Requisitos no funcionales

Permisos mínimos, OIDC sin claves estáticas, secretos en GitHub/SSM, SARIF con retención, sin `latest` como única referencia, sin despliegue desde PR.

## 4. Arquitectura y decisiones

| Ruta | Responsabilidad |
|---|---|
| `.github/workflows/backend-ci-cd.yml` | Pipeline completo |
| `deploy/terraform/` | Infraestructura AWS |
| `Dockerfile` | Imagen multi-stage no root |
| `scripts/deploy-first-time.sh` | Primer despliegue completo desde WSL |
| `scripts/deploy-aws.sh` | Redespliegue por SSM en ambas instancias |

- **BE01-D1:** ECR es el registro principal; DockerHub opcional.
- **BE01-D2:** Deploy solo en push a `main` con environment `production`.
- **BE01-D3:** Terraform validate no ejecuta plan/apply.
- **BE01-D4:** Smoke usa `/health`; no valida Mongo/Redis.

## 5. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| BE01-AC1 | Push/PR ejecuta build y pruebas | Workflow definido; runner pendiente |
| BE01-AC2 | SAST/SCA generan evidencia | Workflow definido; ejecución remota pendiente |
| BE01-AC3 | Terraform válido | `terraform validate` local PASS |
| BE01-AC4 | Imagen publicada y desplegada | NO_EJECUTADO sin AWS |
| BE01-AC5 | Smoke público | NO_EJECUTADO sin URL |

## 6. Plan de pruebas

Validar YAML, shell, Terraform fmt/validate, build/test local y SCA. En GitHub: ejecutar PR, revisar SARIF, publicar imagen, redespliegue SSM y smoke. Sin credenciales no se simula PASS.

## 7. Riesgos y deuda

Caddy es punto único de entrada en la instancia 1 (se acepta por costo cero). Atlas debe permitir las IP `gateway_public_ip` y `worker_public_ip`. Docker Desktop compila la imagen vía `docker.exe`, pero la integración WSL nativa no está habilitada. El primer apply necesita imagen ECR; `user_data` reintenta el pull. El push opcional a DockerHub no está configurado en este entorno.

## Aprobación

| Estado | Revisores | Fecha | Detalle |
|---|---|---|---|
| Implementación local | Agente | 2026-11-03 | YAML, shell y Terraform |
| Aceptación remota | Pendiente | Pendiente | GitHub/AWS |

[Plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).

