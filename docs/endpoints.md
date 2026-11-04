# Endpoints y estado del contrato

Estado 2026-11-03: siete endpoints de evaluación implementados y verificados localmente. `api/openapi.yaml` es el contrato vinculante; este documento es un índice.

| ID | Método y ruta | Expediente | Estado local | Límite |
|---|---|---|---|---|
| USR-01 | `POST /api/v1/auth/register` | [USR-01](../specs/USR-01-register/spec.md) | PASS | Duplicados/rate limit dedicados |
| AUTH-01 | `POST /api/v1/auth/login` | [AUTH-01](../specs/AUTH-01-login/spec.md) | PASS | Bloqueo/timing dedicados |
| AUTH-04 | `POST /api/v1/auth/logout` | [AUTH-04](../specs/AUTH-04-logout/spec.md) | PASS | `all=true` dedicado |
| GRP-01 | `POST /api/v1/groups` | [GRP-01](../specs/GRP-01-create-group/spec.md) | PASS | Moderación de nombre |
| CON-01 | `POST /api/v1/contracts` | [CON-01](../specs/CON-01-create-contract/spec.md) | PASS | Fianza/objetivo/auto-reto |
| CON-01 | `POST /api/v1/groups/{gid}/contracts` | [CON-01](../specs/CON-01-create-contract/spec.md) | PASS | Alias |
| EVI-02 | `POST /api/v1/contracts/{id}/evidence` | [EVI-02](../specs/EVI-02-evidence/spec.md) | PASS local | S3/AWS real |
| TOR-02 | `GET /api/v1/tournaments` | [TOR-02](../specs/TOR-02-list-tournaments/spec.md) | PASS | IDOR/cursor dedicados |
| TOR-02 | `GET /api/v1/groups/{gid}/tournaments` | [TOR-02](../specs/TOR-02-list-tournaments/spec.md) | PASS | Alias |

Soporte adicional: `POST /api/v1/auth/refresh` existe en código, pero no forma parte de los siete endpoints de evaluación ni del contrato OpenAPI actual. Las rutas `/` y `/health` pertenecen a BE-00. Swagger solo se expone en Development/Testing.

