# Plan — 001 Infraestructura y DevSecOps

[Spec](spec.md).

## Enfoque

Terraform modular por archivo (red, seguridad, cómputo, almacenamiento, CDN, parámetros, IAM) con estado remoto; pipeline en jobs paralelos que convergen en una compuerta; imagen multi-stage no root.

## Archivos

| Ruta | Cambio |
|---|---|
| `deploy/bootstrap/main.tf` | Bucket de estado, OIDC, rol de Terraform |
| `deploy/terraform/*.tf` | Infraestructura de la app (backend S3 parcial) |
| `scripts/bootstrap-terraform-pipeline.sh` | Migración única del estado local a S3 |
| `.github/workflows/infra.yml` | Crear/eliminar desde el pipeline |
| `.github/workflows/backend-ci-cd.yml` | CI/CD con CodeQL, Semgrep, Trivy, Dockle y compuerta |
| `database/mongo-init.js` | Script de la base de datos |
| `.dockerignore` | Excluye secretos locales de la imagen |

## Dependencias

Cuenta AWS del equipo (SCP con EC2, ECR, SSM, Rekognition, Comprehend), MongoDB Atlas M0, GitHub `equipopatogg/backend`.

## Pruebas

CI en PR; `terraform plan` sin cambios tras la migración; `infra.yml plan`; smoke `/health`.

## Riesgos

IPs nuevas al recrear (Atlas/app); permisos del rol de Terraform; límites de la capa gratuita.

## Orden

1. Bootstrap y migración de estado.
2. Secretos/Environment en GitHub.
3. `infra.yml plan` → verificación.
4. Documentación y Memoria.
