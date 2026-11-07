# Plan — CON-03 — GET /api/v1/contracts/{id}

[Spec](spec.md).

## Enfoque

Endpoint de Minimal API con validación por `ValidationFilter`, reglas en el servicio del módulo y acceso a datos solo por repositorios (Mongo en producción, memoria en pruebas). Respuestas `{ data, meta }` y errores `{ code, message, request_id }`.
El código se adelantó (v0.5–0.9) y este plan se reconstruyó a partir de él el 2026-11-06 para completar el expediente SDD (constitución §9.3).

## Archivos

| Ruta | Papel |
|---|---|
| `Pactogg.Api/Modules/Contracts/ContractService.cs` | Reglas y servicio |
| `Pactogg.Api/Modules/Contracts/ContractEndpoints.cs` | Endpoint |
| `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoContractRepository.cs` | Repositorio |
| `api/openapi.yaml` | Contrato |

## Pruebas

- `SocialFlowTests`
- `SocialFlowTests.cs`
- `AuthorizationTests.cs`

## Riesgos

Los de la sección de riesgos y deuda de la spec; sin dependencias nuevas.
