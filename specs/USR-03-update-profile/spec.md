# USR-03 — PATCH /api/v1/users/me

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas de integración en verde); despliegue con `scripts/release.sh`.
**Contrato:** `api/openapi.yaml` (v0.5.0)

## 1. Objetivo

Cambiar apodo y/o avatar.

## 2. Requisitos funcionales

- RF1: cuerpo con nickname y/o avatar_id; al menos uno (si no, 422).
- RF2: nickname `^[A-Za-z0-9_.-]{3,24}$` (mismo patrón del registro, D-15).
- RF3: apodo único sin distinguir mayúsculas; conflicto → 409 NICKNAME_ALREADY_EXISTS (también si el índice único gana la carrera).
- RF4: el apodo pasa por moderación de texto; rechazo → 422 CONTENT_REJECTED; moderación caída → 503 (falla cerrada).
- RF5: avatar_id `^[a-z0-9_-]{1,32}$` (avatares predefinidos de la app).
- RF6: actualizar nick_lower en group_members para el autocompletado y auditar `user.profile_updated`.
- RF7: campos desconocidos (p. ej. email) → 400 INVALID_INPUT.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 400 | `INVALID_INPUT` | Campo desconocido o JSON inválido |
| 409 | `NICKNAME_ALREADY_EXISTS` | Apodo en uso |
| 422 | `VALIDATION_ERROR / CONTENT_REJECTED` | Formato o moderación |
| 503 | `MODERATION_UNAVAILABLE` | Moderación caída |

## 4. Implementación

- `Pactogg.Api/Modules/Users/UserService.cs`
- `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoUserRepository.cs (UpdateProfileAsync)`
- `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoGroupMemberRepository.cs (UpdateNickLowerAsync)`

## 5. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| USR03-AC1 | Cambio válido → 200 y GET lo refleja | SocialFlowTests |
| USR03-AC2 | Apodo de otro → 409 | SocialFlowTests |
| USR03-AC3 | Cuerpo vacío → 422; campo email → 400 | SocialFlowTests |
| USR03-AC4 | Apodo moderado → 422 CONTENT_REJECTED | SocialFlowTests |

Pruebas: `IntegrationTest/SocialFlowTests.cs` (fakes en memoria) y `SecurityTest/AuthorizationTests.cs` (401).
