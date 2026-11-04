# Plan — AUTH-01

[Spec](spec.md).

## Enfoque

Normalizar correo, consultar usuario, verificar Argon2id, registrar fallos en Redis, limpiar tras éxito y emitir sesión.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Modules/Auth/AuthEndpoints.cs` | Ruta `POST /login` |
| `Pactogg.Api/Modules/Auth/AuthService.cs` | `LoginAsync` |
| `Pactogg.Api/Infrastructure/Redis/RedisLoginAttemptStore.cs` | Intentos |
| `IntegrationTest/CoreFlowTests.cs` | Flujo |
| `api/openapi.yaml` | Contrato |

## Dependencias

Argon2id, JWT, MongoDB, Redis.

## Pruebas

Login válido, inválido, bloqueo, rate limit y 401.

## Riesgos

Bloqueo y timing sin prueba dedicada; Redis real pendiente.

## Orden

1. DTO y validación.
2. Servicio y persistencia.
3. Endpoint y autorización.
4. Pruebas, OpenAPI y checklist.

## Cierre

Checklist, `Memoria.md` y `specs/validation.md`.

