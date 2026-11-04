# Plan — AUTH-04

[Spec](spec.md).

## Enfoque

Leer claims, revocar `jti`, revocar refresh individual o global y auditar.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Modules/Auth/AuthEndpoints.cs` | Ruta `POST /logout` |
| `Pactogg.Api/Modules/Auth/AuthService.cs` | `LogoutAsync` |
| `Pactogg.Api/Infrastructure/Redis/RedisTokenRevocationStore.cs` | Revocación |
| `IntegrationTest/CoreFlowTests.cs` | Flujo |
| `api/openapi.yaml` | Contrato |

## Dependencias

JWT, Redis, MongoDB.

## Pruebas

Logout 204, uso posterior 401, token inválido y `all=true`.

## Riesgos

`all=true` y refresh revocado sin prueba dedicada; Redis real pendiente.

## Orden

1. DTO y validación.
2. Servicio y persistencia.
3. Endpoint y autorización.
4. Pruebas, OpenAPI y checklist.

## Cierre

Checklist, `Memoria.md` y `specs/validation.md`.

