# Plan — REP-01..06 — Reportes, eliminación de contenido y bloqueos

[Spec](spec.md).

## Enfoque

Endpoint de Minimal API con validación por `ValidationFilter`, reglas en el servicio del módulo y acceso a datos solo por repositorios (Mongo en producción, memoria en pruebas). Respuestas `{ data, meta }` y errores `{ code, message, request_id }`.
El código se adelantó (v0.5–0.9) y este plan se reconstruyó a partir de él el 2026-11-06 para completar el expediente SDD (constitución §9.3).

## Archivos

| Ruta | Papel |
|---|---|
| `Pactogg.Api/Modules/Reports/ReportsModule.cs` | Endpoint |
| `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoReportRepository.cs` | Repositorio |
| `api/openapi.yaml` | Contrato |

## Pruebas

- `ReportsAndBlocksTests.Report_ListAndResolveByRemovingContent`
- `ReportsAndBlocksTests.Report_TargetFromAnotherGroup_Is404_AndUserReportsAreEscalated`
- `ReportsAndBlocksTests.AdminRemovesContentDirectly_MemberCannot`
- `ReportsAndBlocksTests.Block_StopsDirectedChallenges_UntilUnblocked`

## Riesgos

Los de la sección de riesgos y deuda de la spec; sin dependencias nuevas.
