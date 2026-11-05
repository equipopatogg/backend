# 002 — Cuentas y sesión

**Fecha:** 2026-11-04 · **Estado:** Implementado y verificado · **Sprint:** 2
**Historias:** ID 11 Registro con correo · ID 12 Registro mediante Google · ID 13 Inicio de sesión · ID 14 Sesión persistente · ID 15 Cerrar sesión · ID 19 Aceptación de términos y privacidad · ID 21 Apodo único · ID 22 Selección de avatar · ID 158 Autenticación de API.
**Expedientes:** [USR-01](../USR-01-register/spec.md), [AUTH-01](../AUTH-01-login/spec.md), [AUTH-04](../AUTH-04-logout/spec.md), [USR-02](../USR-02-get-profile/spec.md), [USR-03](../USR-03-update-profile/spec.md), [USR-06](../USR-06-check-nickname/spec.md).

## 1. Contexto y objetivo

Que una persona cree su cuenta (correo o Google), entre, mantenga la sesión viva sin volver a escribir su contraseña, cierre sesión, y tenga apodo único y avatar.

## 2. Requisitos funcionales

| ID | Endpoint | Regla clave |
|---|---|---|
| ID 11 | `POST /auth/register` | Correo único, contraseña 12–128 (Argon2id), apodo obligatorio (D-15), privacidad vigente |
| ID 12 | `POST /auth/google` | ID token de Google validado (firma, emisor, audiencia, vigencia, correo verificado); vincula por correo; cuenta nueva exige apodo + privacidad (`422 NICKNAME_REQUIRED`); `503` si no hay Client ID |
| ID 13 | `POST /auth/login` | `401 INVALID_CREDENTIALS` genérico; bloqueo tras 5 fallos (15 min) |
| ID 14 | `POST /auth/refresh` | Access 15 min + refresh 30 días rotativo; reutilizar uno viejo → 401 |
| ID 15 | `POST /auth/logout` | Revoca el access (Redis) y el refresh; `all=true` cierra todos los dispositivos |
| ID 19 | register / google | `accept_privacy=true` y `privacy_version` vigente; se guarda versión y fecha |
| ID 21 | `GET /users/check-nickname`, `PATCH /users/me` | Único sin distinguir mayúsculas, moderado; 20 consultas/min por IP |
| ID 22 | `PATCH /users/me` | `avatar_id` de la lista predefinida |
| ID 158 | Todas | JWT HS256 en `Authorization: Bearer`; política por defecto = autenticado |

## 3. Requisitos no funcionales

Argon2id, sin enumeración de correos en login, límites por IP en register/login/refresh/google, campos desconocidos → 400, HTTPS obligatorio.

## 4. Contrato

`api/openapi.yaml` (tag Auth y Users).

## 5. Arquitectura y decisiones

`Modules/Auth`, `Modules/Users`, `Infrastructure/Security` (`Argon2PasswordHasher`, `JwtTokenService`, `GoogleTokenValidator`, `RedisTokenRevocationStore`, `RedisLoginAttemptStore`).
- 002-D1: el apodo es obligatorio también con Google (D-15).
- 002-D2: cuenta de Google sin contraseña (hash vacío nunca valida en `/auth/login`).
- 002-D3: el Client ID de Google no es secreto; va en `Auth:GoogleClientIds`.

## 6. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| 002-AC1 | Registro, login, refresh y logout de punta a punta | `CoreFlowTests`, `FunctionalFlowTests` |
| 002-AC2 | Google: sin apodo → 422; con apodo → 201; segunda vez → 200; token falso → 401 | `GoogleAuthTests` |
| 002-AC3 | Sin Client ID configurado → 503 (falla cerrada) | `UnitTest/GoogleTokenValidatorTests` |
| 002-AC4 | Apodo duplicado → 409; check-nickname público | `SocialFlowTests` |
| 002-AC5 | Endpoints protegidos sin token → 401 | `SecurityTest/AuthorizationTests` |

## 7. Plan de pruebas

[Plan de pruebas](../../docs/plan-de-pruebas.md), sección Cuentas.

## 8. Riesgos y deuda

Verificación de correo (AUTH-05) y recuperación de contraseña (AUTH-06/07) fuera del sprint. Eliminar cuenta (ID 29) en el sprint 3.

Expediente: [plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).
