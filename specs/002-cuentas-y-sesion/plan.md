# Plan — 002 Cuentas y sesión

[Spec](spec.md).

## Enfoque

Servicio de autenticación con repositorios abstraídos (Mongo en producción, memoria en pruebas), JWT corto + refresh rotativo, Redis para revocación y bloqueos. Google con descubrimiento OpenID y llaves en caché.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Modules/Auth/*` | Registro, login, refresh, logout, Google |
| `Pactogg.Api/Modules/Users/*` | Perfil, apodo, avatar |
| `Pactogg.Api/Infrastructure/Security/GoogleTokenValidator.cs` | Validación del ID token |
| `IntegrationTest/GoogleAuthTests.cs` | Pruebas de Google con validador falso |
| `Pactogg.Api/Modules/Auth/AccountService.cs` | Verificación de correo y recuperación (0.8.0) |
| `Pactogg.Api/Modules/Auth/AuthService.cs` | Hash ficticio para correo inexistente (F2-D51) |
| `Pactogg.Api/Infrastructure/Security/RedisLoginAttemptStore.cs` | Respaldo local del bloqueo (F2-D53) |
| `IntegrationTest/SddDecisionTests.cs` | Pruebas de las decisiones del 2026-11-06 |

## Pruebas

Integración (fakes), seguridad (401), unitarias (Google sin configurar).

## Orden

1. Registro/login/refresh/logout. 2. Perfil y apodo. 3. Google. 4. Verificación y recuperación. 5. Decisiones Q-020 y F2-D53. 6. OpenAPI y guía móvil.
