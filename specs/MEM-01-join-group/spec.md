# MEM-01 — POST /api/v1/groups/join

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas de integración en verde); despliegue con `scripts/release.sh`.
**Contrato:** `api/openapi.yaml` (v0.5.0)

## 1. Objetivo

Unirse a un grupo con su código de invitación.

## 2. Requisitos funcionales

- RF1: code de 6 alfanuméricos; se normaliza a mayúsculas.
- RF2: código inexistente o inactivo → 404.
- RF3: ya miembro → 409 ALREADY_MEMBER (también si el índice único gana la carrera).
- RF4: máximo 5 grupos por usuario → 409 GROUP_LIMIT_REACHED.
- RF5: máximo 20 miembros (`Auth:MaxMembersPerGroup`); incremento atómico condicionado de member_count → 409 GROUP_FULL.
- RF6: alta como member con 100 puntos; si falla el alta se revierte member_count.
- RF7: límite de intentos: 10 cada 10 min por usuario → 429.
- RF8: auditar `group.member_joined`; invalidar caché del usuario y del grupo.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 404 | `NOT_FOUND` | Código inválido |
| 409 | `ALREADY_MEMBER / GROUP_LIMIT_REACHED / GROUP_FULL` | Reglas de cupo |
| 422 | `VALIDATION_ERROR` | Formato de code |
| 429 | `RATE_LIMITED` | Demasiados intentos |

## 4. Implementación

- `Pactogg.Api/Modules/Groups/GroupService.cs (JoinAsync)`
- `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoGroupRepository.cs (TryIncrementMemberCountAsync)`

## 5. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| MEM01-AC1 | Código en minúsculas → 200 member, 100 pts, member_count 2 | SocialFlowTests |
| MEM01-AC2 | Segunda vez → 409 ALREADY_MEMBER | SocialFlowTests |
| MEM01-AC3 | 000000 → 404; ABC → 422 | SocialFlowTests |
| MEM01-AC4 | Grupo con 20 → 409 GROUP_FULL | SocialFlowTests.Join_FullGroup_Returns409 |

Pruebas: `IntegrationTest/SocialFlowTests.cs` (fakes en memoria) y `SecurityTest/AuthorizationTests.cs` (401).
