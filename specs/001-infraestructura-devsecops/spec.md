# 001 — Infraestructura y DevSecOps

**Fecha:** 2026-11-04 · **Estado:** Implementado y desplegado (AWS, costo cero) · **Sprint:** 2
**Fuentes:** [constitución](../../constitution.md), requisitos del docente v3, [Memoria](../../Memoria.md) (F2-D19, F2-D34), CLARIFICACION.
**Expedientes que agrupa:** [BE-00](../BE-00-base-api/spec.md), [BE-01](../BE-01-pipeline-ci/spec.md), [BE-02](../BE-02-api-boundaries/spec.md), [BE-03](../BE-03-quality/spec.md).

## 1. Contexto y objetivo

Dejar la base técnica sobre la que corren todos los endpoints: infraestructura en AWS creada y eliminada con Terraform desde el pipeline, al menos 2 instancias detrás de un balanceador, HTTPS, CDN, observabilidad, Redis, base de datos con script en el repositorio y un pipeline DevSecOps con compuerta de calidad. Todo en capa gratuita.

## 2. Requisitos funcionales

- **RF1 Infraestructura como código.** `deploy/terraform` crea VPC, 2 subredes en 2 zonas, 2 EC2 t3.micro con Elastic IP, Caddy como balanceador (round-robin + health checks), Redis en contenedor, S3 privado + CloudFront (OAC), CloudWatch Logs, SSM Parameter Store, ECR e IAM/OIDC.
- **RF2 Crear y eliminar desde el pipeline.** `.github/workflows/infra.yml` (manual) ejecuta `plan`, `apply` (y publica la imagen + `/health`) o `destroy` (requiere escribir `DESTRUIR`), con estado remoto en S3 (`deploy/bootstrap`) y aprobación del Environment `infra`.
- **RF3 HTTPS.** Let's Encrypt automático vía Caddy sobre `<ip>.sslip.io` (o dominio propio con `domain_name`).
- **RF4 Base de datos.** MongoDB Atlas M0 con TLS; `database/mongo-init.js` crea colecciones, validadores e índices (idempotente); la API verifica índices al arrancar.
- **RF5 Pipeline CI/CD.** `backend-ci-cd.yml`: build + 7 proyectos de prueba, **CodeQL** y **Semgrep** (SAST), NuGet vulnerable + **Trivy** (SCA y secretos), Terraform fmt/validate, **compuerta de calidad** única, imagen Docker con **Trivy** y **Dockle**, despliegue por SSM y smoke.
- **RF6 Ramas.** Cambios por `feature/*` + Pull Request; los PR se prueban y solo `main` despliega ([CONTRIBUTING](../../CONTRIBUTING.md)).

## 3. Requisitos no funcionales

- Costo cero: sin ALB, NAT, ElastiCache ni ASG; S3/CloudFront/CloudWatch dentro de la capa gratuita.
- Sin llaves estáticas en GitHub: OIDC con roles separados (despliegue y Terraform).
- Secretos solo en SSM (SecureString) y GitHub Secrets; nunca en Git ni en la imagen (`.dockerignore`).
- Moderación y almacenamiento fallan cerrados (503).

## 4. Contrato

`GET /health` → `200 Healthy`. Resto en `api/openapi.yaml`.

## 5. Arquitectura y decisiones

| Decisión | Detalle |
|---|---|
| F2-D19 | Costo cero: 2 EC2 + Caddy LB/TLS + Redis en contenedor + S3/CloudFront |
| 001-D1 | Estado de Terraform en S3 con bloqueo nativo (`use_lockfile`); el bucket, el OIDC y el rol de Terraform viven en `deploy/bootstrap` y sobreviven a `destroy` |
| 001-D2 | El rol de Terraform solo lo asume el Environment `infra` (revisores obligatorios) |
| 001-D3 | Semgrep y Dockle fallan solo con severidad ERROR/FATAL para no bloquear por avisos informativos |
| 001-D4 | El despliegue descubre las instancias por etiqueta (`Project=pactogg`, `Role=api`) para sobrevivir a una recreación |

## 6. Criterios de aceptación

| ID | Dado / cuando / entonces | Evidencia |
|---|---|---|
| 001-AC1 | `terraform apply` crea todo y `/health` responde 200 por HTTPS | `https://52-3-200-211.sslip.io/health` |
| 001-AC2 | `infra.yml` con `destroy` y `DESTRUIR` elimina la infraestructura; con `apply` la recrea y publica | Ejecución manual en Actions |
| 001-AC3 | Un PR con pruebas en rojo o hallazgo ERROR/FATAL cierra la compuerta y no despliega | Job `quality-gate` |
| 001-AC4 | `mongo-init.js` deja colecciones e índices iguales a los de la API | `database/mongo-init.js` |
| 001-AC5 | La imagen no contiene secretos locales | `.dockerignore`, Trivy secret scan |

## 7. Plan de pruebas

Ver [plan de pruebas](../../docs/plan-de-pruebas.md): CI en cada PR; smoke tras cada despliegue; `plan` de Terraform antes de `apply`.

## 8. Riesgos, límites y deuda

- Al recrear, las Elastic IP cambian: hay que actualizar la URL en la app y la lista de IPs de Atlas.
- El rol de Terraform tiene permisos amplios (mitigado por OIDC + Environment con revisores + SCP).
- DAST (ZAP) no está en el pipeline.

## Aprobación

| Estado | Revisores | Fecha | Detalle |
|---|---|---|---|
| Implementación | Equipo backend | 2026-11-04 | Desplegado y verificado |
| Validación SDD | José G. | 2026-11-05/06 | [Checklist](checklists/checklist.md) |

Expediente: [plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).
