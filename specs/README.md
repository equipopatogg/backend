# Expedientes Backend PactoGG

Estado 2026-11-04: corte de evaluación desplegado en AWS (costo cero) 11 endpoints sociales (v0.5.0) y 15 de jurado/puntos/compromisos/avisos (v0.6.0) verificados localmente. [Validación](validation.md) · [Preguntas abiertas](open-questions.md) · [Endpoints](../docs/endpoints.md).

## Especificaciones por funcionalidad (SDD, sprint 2)

| Feature | Alcance | Estado |
|---|---|---|
| [001 Infraestructura y DevSecOps](001-infraestructura-devsecops/spec.md) | Terraform (crear/eliminar desde pipeline), CI/CD con CodeQL, Semgrep, Trivy, Dockle y compuerta, script de la BD | Implementado |
| [002 Cuentas y sesión](002-cuentas-y-sesion/spec.md) | IDs 11, 12 (Google), 13, 14, 15, 19, 21, 22, 158 | Implementado |
| [003 Grupos](003-grupos/spec.md) | IDs 30, 31, 32, 159 | Implementado |

Cada feature agrupa los expedientes por endpoint de abajo e incluye spec, plan, tareas y checklist de validación. [Plan de pruebas](../docs/plan-de-pruebas.md).

## Base

| Expediente | Implementación | Límite pendiente |
|---|---|---|
| [BE-00](BE-00-base-api/spec.md) | Host, DI, opciones, middlewares, salud y configuración local | AWS/TLS exterior; Atlas local verificado |
| [BE-01](BE-01-pipeline-ci/spec.md) | Workflow, SAST/SCA, Terraform, Docker/ECR, ASG y smoke | Runner/AWS reales |
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

## Endpoints sociales (v0.5.0)

| Expediente | Endpoint | Implementación |
|---|---|---|
| [USR-02](USR-02-get-profile/spec.md) | `GET /api/v1/users/me` | Perfil con estadísticas, caché 60 s |
| [USR-03](USR-03-update-profile/spec.md) | `PATCH /api/v1/users/me` | Apodo único y moderado, avatar |
| [USR-06](USR-06-check-nickname/spec.md) | `GET /api/v1/users/check-nickname` | Público, 20/min por IP |
| [GRP-02](GRP-02-list-groups/spec.md) | `GET /api/v1/groups` | Mis grupos con q y cursor |
| [GRP-03](GRP-03-get-group/spec.md) | `GET /api/v1/groups/{id}` | Detalle solo para miembros |
| [MEM-01](MEM-01-join-group/spec.md) | `POST /api/v1/groups/join` | Código, 20 miembros, 5 grupos, 10 intentos/10 min |
| [MEM-02](MEM-02-list-members/spec.md) | `GET /api/v1/groups/{id}/members` | Rol, saldo y prefijo de apodo |
| [CON-02](CON-02-list-contracts/spec.md) | `GET /api/v1/groups/{gid}/contracts` | Tablero con status, mine, game, q |
| [CON-03](CON-03-get-contract/spec.md) | `GET /api/v1/contracts/{id}` | Detalle para miembros |
| [CON-06](CON-06-accept-contract/spec.md) | `POST /api/v1/contracts/{id}/accept` | Transacción: estado + fianza + `points_ledger` |
| [CON-07](CON-07-reject-contract/spec.md) | `POST /api/v1/contracts/{id}/reject` | Retado rechaza → `cancelled` |

## Jurado, puntos, compromisos, avisos y Muro de la Fama (v0.6.0)

| Expediente | Endpoint | Historia |
|---|---|---|
| [EVI-03](EVI-03-list-evidence/spec.md) | `GET /api/v1/contracts/{id}/evidence` | ID 87 · Revisión de pruebas por jurado: ver las evidencias de un reto. |
| [VOT-01](VOT-01-pending-votes/spec.md) | `GET /api/v1/me/votes/pending` | ID 88: lista de retos que me toca votar. |
| [VOT-02](VOT-02-cast-vote/spec.md) | `PUT /api/v1/contracts/{id}/votes` | ID 88 · Votación legítimo o trampa; ID 99/100 · entrega o pérdida de fianza. |
| [VOT-03](VOT-03-vote-summary/spec.md) | `GET /api/v1/contracts/{id}/votes/summary` | Avance de la votación sin revelar quién votó qué. |
| [VOT-04](VOT-04-resolve-voting/spec.md) | `POST /api/v1/contracts/{id}/votes/resolve` | El administrador resuelve un empate, una votación vencida o un reto sin jurados. |
| [VOT-05](VOT-05-contract-result/spec.md) | `GET /api/v1/contracts/{id}/result` | Resultado final con los votos revelados. |
| [PTS-01](PTS-01-my-points/spec.md) | `GET /api/v1/groups/{gid}/points/me` | ID 159 · Puntos sin valor monetario: saldo y movimientos propios. |
| [PTS-02](PTS-02-points-ranking/spec.md) | `GET /api/v1/groups/{gid}/points/ranking` | Ranking del grupo por puntos. |
| [COM-01](COM-01-list-commitments/spec.md) | `GET /api/v1/groups/{gid}/commitments` | ID 124/125 · Compromisos por cumplir y a mi favor. |
| [COM-03](COM-03-complete-commitment/spec.md) | `POST /api/v1/commitments/{id}/complete` | El acreedor confirma que le cumplieron. |
| [COM-04](COM-04-request-complete/spec.md) | `POST /api/v1/commitments/{id}/request-complete` | El deudor avisa que ya cumplió. |
| [NOT-01](NOT-01-list-notifications/spec.md) | `GET /api/v1/notifications` | ID 69 · Aviso de reto recibido (dentro de la app). |
| [NOT-02](NOT-02-read-notification/spec.md) | `PATCH /api/v1/notifications/{id}/read` | Marcar un aviso como leído. |
| [NOT-03](NOT-03-read-all/spec.md) | `POST /api/v1/notifications/read-all` | Marcar todos como leídos. |
| [ACH-01](ACH-01-hall-of-fame/spec.md) | `GET /api/v1/groups/{gid}/fame` | ID 132 · Muro de la Fama. |

Cada expediente conserva spec, plan, tareas y checklist con IDs. Una casilla completa significa verificación local, no aceptación institucional. Las rutas alias se documentan en el expediente principal. `api/openapi.yaml` es el contrato único.

