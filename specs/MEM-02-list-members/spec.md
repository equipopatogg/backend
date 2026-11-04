# MEM-02 — GET /api/v1/groups/{id}/members

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas de integración en verde); despliegue con `scripts/release.sh`.
**Contrato:** `api/openapi.yaml` (v0.5.0)

## 1. Objetivo

Lista de miembros con rol y saldo; autocompletado del rival.

## 2. Requisitos funcionales

- RF1: solo miembros (403) de un grupo existente (404).
- RF2: q = prefijo del apodo sobre nick_lower (regex anclada y escapada, índice ix_group_members_group_nick).
- RF3: orden de ingreso (_id ascendente) con cursor; limit 1–50.
- RF4: cada elemento: user_id, nickname, avatar_id, role, points_balance, joined_at.
- RF5: caché 60 s por versión del grupo (se invalida al unirse alguien, aceptar retos o cambiar un apodo).

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_GROUP_MEMBER` | No pertenece |
| 404 | `NOT_FOUND` | No existe |
| 422 | `VALIDATION_ERROR` | Parámetros |

## 4. Implementación

- `Pactogg.Api/Modules/Groups/GroupService.cs (ListMembersAsync)`
- `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoGroupMemberRepository.cs (ListByGroupAsync)`

## 5. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| MEM02-AC1 | 2 miembros, admin primero | SocialFlowTests |
| MEM02-AC2 | Prefijo encuentra al dueño | SocialFlowTests |
| MEM02-AC3 | Paginación limit=1 con cursor | SocialFlowTests |
| MEM02-AC4 | Ajeno → 403 | SocialFlowTests |

Pruebas: `IntegrationTest/SocialFlowTests.cs` (fakes en memoria) y `SecurityTest/AuthorizationTests.cs` (401).
