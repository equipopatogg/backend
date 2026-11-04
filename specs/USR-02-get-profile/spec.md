# USR-02 — GET /api/v1/users/me

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas de integración en verde); despliegue con `scripts/release.sh`.
**Contrato:** `api/openapi.yaml` (v0.5.0)

## 1. Objetivo

Devolver el perfil propio con estadísticas para la pantalla Perfil.

## 2. Requisitos funcionales

- RF1: exigir token válido.
- RF2: devolver id, email, nickname, avatar_id, email_verified, stats (completed, cheated, streak, trophies, word_index) y groups_count.
- RF3: caché Redis 60 s por usuario con llave versionada; se invalida al editar el perfil, crear o unirse a un grupo y al aceptar un reto.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 401 | `UNAUTHORIZED` | Sin token o usuario eliminado |

## 4. Implementación

- `Pactogg.Api/Modules/Users/UserEndpoints.cs`
- `Pactogg.Api/Modules/Users/UserService.cs`
- `Pactogg.Api/Modules/Users/Dtos/UserDtos.cs`
- `Pactogg.Api/Infrastructure/Redis/CacheExtensions.cs`

## 5. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| USR02-AC1 | Perfil recién registrado: word_index 50, groups_count 0 | SocialFlowTests.Profile_GetUpdateAndCheckNickname |
| USR02-AC2 | Tras PATCH el GET refleja el cambio (caché invalidada) | SocialFlowTests |
| USR02-AC3 | Sin token → 401 | SecurityTest.AuthorizationTests |

Pruebas: `IntegrationTest/SocialFlowTests.cs` (fakes en memoria) y `SecurityTest/AuthorizationTests.cs` (401).
