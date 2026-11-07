# Plan — PTS-03, COM-02, COM-05 y ACH-03 — Detalles y resúmenes

[Spec](spec.md).

## Enfoque

Endpoint de Minimal API con validación por `ValidationFilter`, reglas en el servicio del módulo y acceso a datos solo por repositorios (Mongo en producción, memoria en pruebas). Respuestas `{ data, meta }` y errores `{ code, message, request_id }`.
El código se adelantó (v0.5–0.9) y este plan se reconstruyó a partir de él el 2026-11-06 para completar el expediente SDD (constitución §9.3).

## Archivos

| Ruta | Papel |
|---|---|
| `Pactogg.Api/Modules/Points/PointsEndpoints.cs` | Endpoint |
| `Pactogg.Api/Modules/Points/PointsService.cs` | Reglas y servicio |
| `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoPointsLedgerRepository.cs` | Repositorio |
| `api/openapi.yaml` | Contrato |

## Pruebas

- `DetailEndpointsTests.PointsMovement_OnlyOwnerOrAdmin_AndRankingHasKing`
- `DetailEndpointsTests.CommitmentDetailAndSummary`

## Riesgos

Los de la sección de riesgos y deuda de la spec; sin dependencias nuevas.
