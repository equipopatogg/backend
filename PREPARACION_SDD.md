# Entrada SDD Backend PactoGG

Estado 2026-11-03: implementación local del corte vertical de evaluación. Leer [AGENTS](AGENTS.md), [constitución](constitution.md), [fase](.specify/memory/constitution.md) y [validación](specs/validation.md).

1. [BE-00](specs/BE-00-base-api/spec.md): host, configuración, middlewares, salud y Atlas local.
2. [BE-01](specs/BE-01-pipeline-ci/spec.md): CI/CD, Terraform, Docker y despliegue AWS.
3. [BE-02](specs/BE-02-api-boundaries/spec.md): DTO, errores, validación y autorización.
4. [BE-03](specs/BE-03-quality/spec.md): build, pruebas, SCA y evidencia.
5. [USR-01](specs/USR-01-register/spec.md), [AUTH-01](specs/AUTH-01-login/spec.md), [AUTH-04](specs/AUTH-04-logout/spec.md), [GRP-01](specs/GRP-01-create-group/spec.md), [CON-01](specs/CON-01-create-contract/spec.md), [EVI-02](specs/EVI-02-evidence/spec.md) y [TOR-02](specs/TOR-02-list-tournaments/spec.md): un expediente por endpoint.

Cada expediente conserva spec, plan, tareas y checklist. Las ayudas de `.opencode` son de lectura. `api/openapi.yaml` es el contrato; `docs/endpoints.md` solo indexa. AWS, Atlas real, TLS exterior y aceptación humana siguen pendientes.

