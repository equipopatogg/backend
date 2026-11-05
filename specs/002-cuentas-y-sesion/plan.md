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

## Pruebas

Integración (fakes), seguridad (401), unitarias (Google sin configurar).

## Orden

1. Registro/login/refresh/logout. 2. Perfil y apodo. 3. Google. 4. OpenAPI y guía móvil.
