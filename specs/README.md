# Expedientes Backend PactoGG

Estado 2026-11-04: corte de evaluación desplegado en AWS (costo cero) 11 endpoints sociales (v0.5.0) y 15 de jurado/puntos/compromisos/avisos (v0.6.0) verificados localmente. [Validación](validation.md) · [Preguntas abiertas](open-questions.md) · [Endpoints](../docs/endpoints.md).

## Especificaciones por funcionalidad (SDD, sprints 2 y 3)

| Feature | Alcance | Estado |
|---|---|---|
| [001 Infraestructura y DevSecOps](001-infraestructura-devsecops/spec.md) | Terraform (crear/eliminar desde pipeline), CI/CD con CodeQL, Semgrep, Trivy, Dockle y compuerta, script de la BD | Implementado |
| [002 Cuentas y sesión](002-cuentas-y-sesion/spec.md) | IDs 11, 12 (Google), 13, 14, 15, 19, 21, 22, 158 | Implementado |
| [003 Grupos](003-grupos/spec.md) | IDs 30, 31, 32, 159 | Implementado |
| [004 Contratos y demo](004-contratos-y-demo/spec.md) | IDs 42, 43, 44, 70, 160 (demo 1 y 5 solo en la app) | Implementado salvo 004-T8 (salida del creador) |
| [005 Contrato por voz con IA](005-contrato-por-voz/spec.md) | IDs 56–60, CON-08 | Spec lista; código pendiente del modelo de IA (Q-007) |
| [006 Push, permisos y sin conexión](006-push-permisos-offline/spec.md) | IDs 69, 143, 147, permisos, Room | Backend implementado salvo 006-T7..T10; parte móvil pendiente |

[Validación SDD 001–003](validacion-001-003.md) (2026-11-06): las tres specs cumplen CLARIFICACION #138; aprobadas por el PO; falta la revisión de Kevin y José.

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
| [AUTH-01](AUTH-01-login/spec.md) | `POST /api/v1/auth/login` | Login, bloqueo y hash ficticio para correo inexistente (F2-D51) | Prueba dedicada de bloqueo |
| [AUTH-04](AUTH-04-logout/spec.md) | `POST /api/v1/auth/logout` | Revocación | `all=true` dedicado |
| [GRP-01](GRP-01-create-group/spec.md) | `POST /api/v1/groups` | Grupo, admin, código y nombre moderado (F2-D52) | — |
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

## Cuenta, retos, auditoría y push (v0.8.0)

| Expediente | Endpoint | Historia |
|---|---|---|
| [AUTH-05](AUTH-05-verify-email/spec.md) | `POST /api/v1/auth/verify-email`, `POST /api/v1/auth/verify-email/resend` | ID 17 · Correo de verificación |
| [AUTH-06/07](AUTH-06-07-password-recovery/spec.md) | `POST /api/v1/auth/forgot-password`, `POST /api/v1/auth/reset-password`, `POST /api/v1/users/me/password` | ID 16 · Recuperar contraseña; cambio de contraseña |
| [USR-05/07](USR-05-public-profile/spec.md) | `GET /api/v1/users/{id}`, `POST /api/v1/users/me/consent` | ID 23/25 perfil e Índice de Palabra; ID 19 privacidad |
| [CON-04/05](CON-04-05-edit-cancel/spec.md) | `PATCH` / `DELETE /api/v1/contracts/{id}` | ID 43 · Publicar reto (editar y cancelar) |
| [AUD-01](AUD-01-group-audit/spec.md) | `GET /api/v1/groups/{gid}/audit` | ID 160 · Historial inmutable |
| [DEV-01..03, NOT-04](DEV-01-03-push/spec.md) | `PUT`/`DELETE /api/v1/devices/me`, `GET`/`PATCH /api/v1/devices/me/preferences`, `DELETE /api/v1/notifications/{id}` | ID 143 · Push; ID 69 · Aviso de reto recibido; ID 147 · Centro de notificaciones |

Cada expediente conserva spec, plan, tareas y checklist con IDs. Una casilla completa significa verificación local, no aceptación institucional. Las rutas alias se documentan en el expediente principal. `api/openapi.yaml` es el contrato único.

## Administración, cuenta, reportes y detalles (v0.9.0)

| Expediente | Endpoints | Historia |
|---|---|---|
| [GRP-04..07, MEM-03..06](GRP-04-07-MEM-03-06-group-admin/spec.md) | Editar y eliminar grupo, regenerar y activar código, roles, expulsar, salir, mi membresía | ID 30–32 (administración) |
| [USR-04](USR-04-delete-account/spec.md) | `DELETE /api/v1/users/me` | ID 29 · Eliminación de cuenta y datos |
| [REP-01..06](REP-01-06-reports-blocks/spec.md) | Reportes, quitar contenido, bloquear | ID 149, ID 153 |
| [PTS-03, COM-02, COM-05, ACH-03](PTS-03-COM-02-05-ACH-03-details/spec.md) | Movimiento, compromiso, resumen, ranking y Rey del Grupo | ID 124, 125, 159 |

Desde el 2026-11-06 los 50 expedientes tienen spec, plan, tareas y checklist.

