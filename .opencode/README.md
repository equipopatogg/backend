# Ayudas locales Backend PactoGG

Cinco perfiles de lectura y cuatro skills propias. Son documentos de apoyo, no procesos activos. Rigen [AGENTS](../AGENTS.md), [constitución](../constitution.md) y [fase](../.specify/memory/constitution.md).

## Perfiles

| Rol | Perfil | Propuesta o revisión |
|---|---|---|
| foundation-builder | [base-planner](agents/pactogg-backend-base-planner.md) | Base BE-00, DI, opciones y configuración |
| slice-scaffolder | [change-planner](agents/pactogg-backend-change-planner.md) | Cambio mínimo por endpoint |
| security-reviewer | [security-reviewer](agents/pactogg-backend-security-reviewer.md) | IDOR, secretos, moderación y abuso |
| traceability-clerk | [traceability-reviewer](agents/pactogg-backend-traceability-reviewer.md) | Spec, tareas, criterios y evidencia |
| readme-keeper | [docs-keeper](agents/pactogg-backend-docs-keeper.md) | Índices, memoria y estados |

Todos declaran `mode: subagent` y deniegan `edit`, `bash` y `task`. Leen y proponen; no escriben ni ejecutan.

## Selección contextual

| Expediente | Perfil principal | Skill |
|---|---|---|
| [BE-00](../specs/BE-00-base-api/spec.md) | base-planner / security-reviewer | [foundation](skills/pactogg-backend-foundation/SKILL.md) |
| [BE-01](../specs/BE-01-pipeline-ci/spec.md) | change-planner / security-reviewer | [ci-quality](skills/pactogg-backend-ci-quality/SKILL.md) |
| [BE-02](../specs/BE-02-api-boundaries/spec.md) | change-planner / security-reviewer | [api-boundaries](skills/pactogg-backend-api-boundaries/SKILL.md) |
| [BE-03](../specs/BE-03-quality/spec.md) | traceability-reviewer / change-planner | [ci-quality](skills/pactogg-backend-ci-quality/SKILL.md) |
| Endpoints USR/AUTH/GRP/CON/EVI/TOR | change-planner / security-reviewer | [endpoint-slices](skills/pactogg-backend-endpoint-slices/SKILL.md) |

Esta tabla orienta la lectura, no una aprobación técnica. La implementación y los reportes locales no sustituyen AWS, Atlas real, TLS exterior ni revisión humana.

