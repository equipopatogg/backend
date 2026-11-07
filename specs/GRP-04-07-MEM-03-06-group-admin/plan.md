# Plan — GRP-04..07 y MEM-03..06 — Administración del grupo y miembros

[Spec](spec.md).

## Enfoque

Endpoint de Minimal API con validación por `ValidationFilter`, reglas en el servicio del módulo y acceso a datos solo por repositorios (Mongo en producción, memoria en pruebas). Respuestas `{ data, meta }` y errores `{ code, message, request_id }`.
El código se adelantó (v0.5–0.9) y este plan se reconstruyó a partir de él el 2026-11-06 para completar el expediente SDD (constitución §9.3).

## Archivos

| Ruta | Papel |
|---|---|
| `Pactogg.Api/Modules/Groups/GroupEndpoints.cs` | Endpoint |
| `Pactogg.Api/Modules/Groups/GroupService.cs` | Reglas y servicio |
| `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoGroupRepository.cs` | Repositorio |
| `api/openapi.yaml` | Contrato |

## Pruebas

- `GroupAdminTests.Admin_EditsNameAndSettings_MemberGets403`
- `GroupAdminTests.RegenerateAndDisableInviteCode`
- `GroupAdminTests.Roles_CreatorCannotBeTouched_PromotedMemberIsNotified`
- `GroupAdminTests.KickedMember_ContractsCancelledBondRefunded_CanRejoinWithBalance`
- `GroupAdminTests.LeaveAndMyMembership`
- `GroupAdminTests.OwnerDeletesGroup_CancelsContractsAndFreesMembers`

## Riesgos

Los de la sección de riesgos y deuda de la spec; sin dependencias nuevas.
