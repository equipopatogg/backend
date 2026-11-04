# Expedientes Backend PactoGG

Estado 2026-11-03: corte vertical de evaluación implementado y verificado localmente; AWS, despliegue y aceptación externa pendientes; Atlas verificado localmente. [Validación](validation.md) · [Preguntas abiertas](open-questions.md) · [Endpoints](../docs/endpoints.md).

## Base

| Expediente | Implementación | Límite pendiente |
|---|---|---|
| [BE-00](BE-00-base-api/spec.md) | Host, DI, opciones, middlewares, salud y configuración local | AWS/TLS exterior; Atlas local verificado |
| [BE-01](BE-01-pipeline-ci/spec.md) | Workflow, SAST/SCA, Terraform de costo cero, Docker/ECR, redespliegue SSM y smoke | Runner/AWS reales |
| [BE-02](BE-02-api-boundaries/spec.md) | JSON, errores, validación, claims y NoSQL | Compatibilidad Android |
| [BE-03](BE-03-quality/spec.md) | Build, suites, SCA y Terraform validate | Docker/AWS/cobertura |

## Endpoints de evaluación

| Expediente | Endpoint | Implementación | Límite pendiente |
|---|---|---|---|
| [USR-01](USR-01-register/spec.md) | `POST /api/v1/auth/register` | Registro y sesión | Duplicados/rate limit dedicados |
| [AUTH-01](AUTH-01-login/spec.md) | `POST /api/v1/auth/login` | Login y bloqueo | Pruebas de bloqueo/timing |
| [AUTH-04](AUTH-04-logout/spec.md) | `POST /api/v1/auth/logout` | Revocación | `all=true` dedicado |
| [GRP-01](GRP-01-create-group/spec.md) | `POST /api/v1/groups` | Grupo, admin y código | Moderación de nombre |
| [CON-01](CON-01-create-contract/spec.md) | `POST /api/v1/contracts` + alias anidado | Contrato y moderación | Fianza/objetivo/auto-reto |
| [EVI-02](EVI-02-evidence/spec.md) | `POST /api/v1/contracts/{id}/evidence` | Enlace/foto y S3 | S3/AWS real, transición |
| [TOR-02](TOR-02-list-tournaments/spec.md) | `GET /api/v1/tournaments` + alias anidado | Lista, filtros y bracket | IDOR/cursor dedicados |

Cada expediente conserva spec, plan, tareas y checklist con IDs. Una casilla completa significa verificación local, no aceptación institucional. Las rutas alias se documentan en el expediente principal. `api/openapi.yaml` es el contrato único.

