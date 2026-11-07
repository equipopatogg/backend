# 001 — Infraestructura y DevSecOps

**Fecha:** 2026-11-06 · **Estado:** Aprobada, implementada y desplegada (AWS, costo cero) · **Sprint:** 2
**Fuentes:** [constitución](../../constitution.md) §4, §5, §9 y §12; requisitos del docente v3; CLARIFICACION #129–#137; [Memoria](../../Memoria.md) (F2-D19, F2-D34, F2-D40–F2-D44).
**Expedientes que agrupa:** [BE-00](../BE-00-base-api/spec.md), [BE-01](../BE-01-pipeline-ci/spec.md), [BE-02](../BE-02-api-boundaries/spec.md), [BE-03](../BE-03-quality/spec.md).

## 1. Contexto y objetivo

Dejar la base técnica sobre la que corren todos los endpoints: infraestructura en AWS creada y eliminada con Terraform desde el pipeline, 2 instancias detrás de un balanceador, HTTPS, CDN, observabilidad, Redis, base de datos con script en el repositorio y un pipeline DevSecOps con compuerta de calidad. Todo en capa gratuita o con créditos.

## 2. Fuera de alcance

- ALB, NAT Gateway, ElastiCache y Auto Scaling (cuestan; F2-D19).
- AWS WAF, MFA para administradores y firma de imágenes con Cosign (refuerzos opcionales si alcanzan los créditos).
- Dominio propio: se usa `sslip.io`.
- Pruebas de mutación como bloqueo de la compuerta (opcional en la constitución).

## 3. Requisitos funcionales (EARS)

| ID | Requisito |
|---|---|
| 001-R1 | Cuando se ejecute `terraform apply`, el sistema deberá crear VPC con 2 subredes en 2 zonas, 2 EC2 t3.micro con Elastic IP, Caddy como balanceador con health checks, Redis en contenedor, S3 privado + CloudFront con OAC, CloudWatch Logs, SSM Parameter Store, ECR e IAM con OIDC. |
| 001-R2 | Cuando un revisor apruebe `infra.yml` con `plan`, `apply` o `destroy` (este último escribiendo `DESTRUIR`), el pipeline deberá ejecutar esa acción con estado remoto en S3 y bloqueo. |
| 001-R3 | Mientras la API esté desplegada, el sistema deberá servir HTTPS con certificado Let's Encrypt renovado automáticamente por Caddy. |
| 001-R4 | Si una instancia no responde `/health`, entonces Caddy deberá dejar de enviarle tráfico y usar la otra. |
| 001-R5 | Cuando arranque la API, el sistema deberá verificar o crear los índices de MongoDB; `database/mongo-init.js` deberá dejar las mismas colecciones, validadores e índices. |
| 001-R10 | Si la base de datos no responde, entonces `/health/ready` deberá responder `503` sin revelar hosts ni cadenas de conexión; `/health` sigue indicando solo que el proceso vive (F2-D54, CLARIFICACION #136). |
| 001-R6 | Cuando se abra o actualice un Pull Request, el pipeline deberá ejecutar build, las suites de prueba, SAST (CodeQL, Semgrep, SonarCloud si hay token), SCA y SBOM (NuGet vulnerable, Trivy, CycloneDX, Dependabot), secretos (Gitleaks), IaC (Checkov), imagen (Trivy, Dockle) y la compuerta `quality-gate`. |
| 001-R7 | Si hay pruebas en rojo o hallazgos críticos o altos, entonces la compuerta deberá bloquear el despliegue. |
| 001-R8 | Cuando se haga merge a `main` y la compuerta pase, el pipeline deberá publicar la imagen en ECR, redesplegar ambas instancias por SSM, ejecutar el smoke y después DAST con OWASP ZAP. |
| 001-R9 | Mientras exista la cuenta, el bootstrap deberá mantener AWS Budgets con alertas al 50, 80 y 100 % y un trail de CloudTrail. |

## 4. Requisitos no funcionales

- Costo cero salvo créditos de AWS; sin llaves estáticas en GitHub (OIDC con roles separados).
- Secretos solo en SSM SecureString y GitHub Secrets; nunca en Git ni en la imagen (`.dockerignore`).
- Acciones de GitHub fijadas por SHA; `GITHUB_TOKEN` con permisos mínimos.
- Logs JSON con `request_id` y retención de 30 días.
- Recuperación objetivo: 1 hora con `infra.yml apply` + `mongo-init.js`.

## 5. Endpoints y esquemas

`GET /health` → `200 Healthy` (vida del proceso, la usa Caddy). `GET /health/ready` → `200` o `503` si la base no responde. `GET /` → nombre y versión. El resto en `api/openapi.yaml`.

## 6. Criterios de aceptación

| ID | Requisito | Dado / cuando / entonces | Evidencia |
|---|---|---|---|
| 001-AC1 | R1, R3 | `terraform apply` crea todo y `/health` responde 200 por HTTPS | `https://52-3-200-211.sslip.io/health` |
| 001-AC2 | R2 | `destroy` con `DESTRUIR` elimina; `apply` recrea y publica | Ejecución manual de `infra.yml` |
| 001-AC3 | R6, R7 | Un PR con pruebas en rojo o hallazgo alto cierra la compuerta | Job `quality-gate` (PR #2, run #10) |
| 001-AC4 | R5 | `mongo-init.js` deja colecciones e índices iguales a los de la API | `DatabaseTest/MongoIndexInitializerTests` |
| 001-AC5 | R6 | La imagen no contiene secretos locales | `.dockerignore`, Trivy, Gitleaks |
| 001-AC6 | R4 | Con una instancia detenida, la API sigue respondiendo | Prueba manual pendiente (verificación de dos instancias, Memoria) |
| 001-AC7 | R8 | Tras el merge, smoke y ZAP corren contra la URL pública | Workflow `backend-ci-cd.yml`, run de `main` |
| 001-AC9 | R10 | Con la base inaccesible, `/health/ready` → 503 y `/health` → 200 | `HealthWithoutDatabaseTests.HealthReady_WhenDatabaseIsDown_Returns503` |
| 001-AC8 | R9 | Budgets y CloudTrail existen tras `deploy/bootstrap` | Pendiente de `terraform apply` en bootstrap |

## 7. Preguntas abiertas

Ninguna. Q-006 (Redis) y Q-009 (.NET) resueltas.

## 8. Verificación contra la constitución

| Regla | Cumple |
|---|---|
| §4 stack y "NO se usa" | Sí |
| §5.1 dos instancias intercambiables detrás de Caddy, Redis compartido | Sí |
| §9.2 pipeline obligatorio completo | Sí; mutación opcional |
| §12 Budgets 50/80/100 % y apagado fuera de uso | Sí en código; falta aplicar el bootstrap |
| §3.7 sin secretos en el repositorio | Sí |

## 9. Plan de pruebas

[Plan de pruebas](../../docs/plan-de-pruebas.md): CI en cada PR; smoke y ZAP tras cada despliegue; `plan` antes de `apply`.

## 10. Riesgos y deuda

- Al recrear, las Elastic IP cambian: hay que actualizar la URL de la app y la lista de IPs de Atlas.
- El rol de Terraform tiene permisos amplios (mitigado con OIDC, Environment con revisores y SCP).
- 001-AC6 y 001-AC8 dependen de acciones manuales del equipo.
- Caddy sigue revisando `/health`; pasarlo a `/health/ready` cambia `user_data` y reemplaza las instancias, así que se hará junto con la configuración de SMTP y Firebase (001-T9).

## Aprobación

| Rol | Persona | Fecha | Resultado |
|---|---|---|---|
| Product Owner | David M. | 2026-11-06 | Aprobada |
| Revisión técnica | Kevin H. | Pendiente | — |
| Comprobabilidad | José G. | Pendiente | — |

Expediente: [plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).
