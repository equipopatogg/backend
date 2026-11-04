# Plan — USR-01

[Spec](spec.md).

## Enfoque

DTO validado, servicio con normalización/unicidad, Argon2id, creación de usuario y sesión JWT.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Modules/Auth/AuthEndpoints.cs` | Ruta `POST /register` |
| `Pactogg.Api/Modules/Auth/AuthService.cs` | `RegisterAsync` |
| `Pactogg.Api/Modules/Auth/Dtos/AuthDtos.cs` | `RegisterRequest` y `SessionResponse` |
| `IntegrationTest/CoreFlowTests.cs` | Flujo |
| `api/openapi.yaml` | Contrato |

## Dependencias

Argon2id, JWT, MongoDB, Redis para rate limit/revocación.

## Pruebas

Registro válido, duplicados, consentimiento, campo desconocido y hasher.

## Riesgos

Duplicados/rate limit sin prueba dedicada; E2E Atlas pendiente.

## Orden

1. DTO y validación.
2. Servicio y persistencia.
3. Endpoint y autorización.
4. Pruebas, OpenAPI y checklist.

## Cierre

Checklist, `Memoria.md` y `specs/validation.md`.

