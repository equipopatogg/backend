# Plan — MEM-01 — POST /api/v1/groups/join

[Spec](spec.md).

## Enfoque

Endpoint de Minimal API con validación por `ValidationFilter`, reglas en el servicio del módulo y acceso a datos solo por repositorios (Mongo en producción, memoria en pruebas). Respuestas `{ data, meta }` y errores `{ code, message, request_id }`.
El código se adelantó (v0.5–0.9) y este plan se reconstruyó a partir de él el 2026-11-06 para completar el expediente SDD (constitución §9.3).

## Archivos

| Ruta | Papel |
|---|---|
| `Pactogg.Api/Modules/Groups/GroupService.cs` | Reglas y servicio |
| `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoGroupRepository.cs` | Repositorio |
| `Pactogg.Api/Modules/Groups/GroupEndpoints.cs` | Endpoint |
| `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoGroupMemberRepository.cs` | Repositorio |
| `api/openapi.yaml` | Contrato |

## Pruebas

- `SocialFlowTests`
- `SocialFlowTests.Join_FullGroup_Returns409`
- `SocialFlowTests.cs`
- `AuthorizationTests.cs`

## Riesgos

Los de la sección de riesgos y deuda de la spec; sin dependencias nuevas.
