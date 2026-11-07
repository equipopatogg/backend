# 002 — Cuentas y sesión

**Fecha:** 2026-11-06 · **Estado:** Aprobada e implementada · **Sprint:** 2 (verificación y recuperación adelantadas del 3)
**Historias:** ID 11 Registro con correo · ID 12 Registro mediante Google · ID 13 Inicio de sesión · ID 14 Sesión persistente · ID 15 Cerrar sesión · ID 16 Recuperar contraseña · ID 17 Correo de verificación · ID 19 Aceptación de términos y privacidad · ID 21 Apodo único · ID 22 Selección de avatar · ID 158 Autenticación de API.
**Expedientes:** [USR-01](../USR-01-register/spec.md), [AUTH-01](../AUTH-01-login/spec.md), [AUTH-02](../AUTH-02-google/spec.md), [AUTH-04](../AUTH-04-logout/spec.md), [AUTH-05](../AUTH-05-verify-email/spec.md), [AUTH-06/07](../AUTH-06-07-password-recovery/spec.md), [USR-02](../USR-02-get-profile/spec.md), [USR-03](../USR-03-update-profile/spec.md), [USR-05/07](../USR-05-public-profile/spec.md), [USR-06](../USR-06-check-nickname/spec.md).
**Fuentes:** constitución §6.1 y §8; CLARIFICACION #4–#13 y #138; CRUD hojas Usuarios y Sesiones.

## 1. Contexto y objetivo

Que una persona cree su cuenta (correo o Google), verifique su correo, entre, mantenga la sesión viva sin volver a escribir su contraseña, la recupere si la olvida, cierre sesión y tenga apodo único y avatar.

## 2. Fuera de alcance

- Cambiar el correo de la cuenta (CLARIFICACION #13: no se cambia en la primera versión).
- Eliminar la cuenta (USR-04, ID 29): sprint 3, expediente propio.
- MFA y certificate pinning (refuerzos opcionales, CLARIFICACION de seguridad).
- Envío real de correos en producción mientras no exista proveedor SMTP configurado (el código funciona; falta la credencial).

## 3. Requisitos funcionales (EARS)

| ID | Requisito | Endpoint |
|---|---|---|
| 002-R1 | Cuando una persona envíe correo, contraseña de 12–128 caracteres, apodo y `accept_privacy=true` con la versión vigente, el sistema deberá crear la cuenta con la contraseña en Argon2id y responder `201` con la sesión. | USR-01 |
| 002-R2 | Si el correo o el apodo ya existen (sin distinguir mayúsculas), entonces el sistema deberá responder `409`. | USR-01 |
| 002-R3 | Cuando la app envíe un ID token de Google válido (firma, emisor, audiencia, vigencia y correo verificado), el sistema deberá iniciar sesión o crear la cuenta; si la cuenta es nueva y falta el apodo o la privacidad, deberá responder `422 NICKNAME_REQUIRED`. | AUTH-02 |
| 002-R4 | Si no hay Client ID de Google configurado, entonces el sistema deberá responder `503` (falla cerrada). | AUTH-02 |
| 002-R5 | Cuando las credenciales sean correctas, el sistema deberá entregar un access JWT de 15 minutos y un refresh rotativo de 30 días. | AUTH-01 |
| 002-R6 | Si las credenciales son incorrectas o el correo no existe, entonces el sistema deberá responder `401 INVALID_CREDENTIALS` con el mismo mensaje y el mismo trabajo de hash (F2-D51). | AUTH-01 |
| 002-R7 | Mientras haya 5 fallos de login para el mismo correo e IP en 15 minutos, el sistema deberá responder `429` aunque la contraseña sea correcta, también si Redis no está disponible (F2-D53). | AUTH-01 |
| 002-R8 | Cuando se use un refresh válido, el sistema deberá emitir un par nuevo y marcar el anterior como rotado. | AUTH-03 |
| 002-R9 | Si reaparece un refresh ya rotado, entonces el sistema deberá revocar todas las sesiones del usuario (F2-D49). | AUTH-03 |
| 002-R10 | Cuando el usuario cierre sesión, el sistema deberá revocar el access actual en Redis y el refresh; con `all=true` deberá revocar todas sus sesiones; respuesta `204`. | AUTH-04 |
| 002-R11 | Cuando el usuario abra el enlace de verificación (token de un solo uso, hasheado, 24 h), el sistema deberá marcar el correo como verificado. | AUTH-05 |
| 002-R12 | Cuando alguien pida recuperar la contraseña, el sistema deberá responder igual exista o no la cuenta y, si existe, enviar un token de un solo uso de 1 hora; al usarlo deberá cambiar la contraseña y revocar todas las sesiones. | AUTH-06, AUTH-07 |
| 002-R13 | Cuando el usuario consulte o cambie su perfil, el sistema deberá permitir editar apodo (3–24, único, moderado) y `avatar_id` de la lista predefinida (F2-D50). | USR-02, USR-03 |
| 002-R14 | Cuando alguien consulte un apodo, el sistema deberá indicar si está libre, con límite de 20 consultas por minuto por IP. | USR-06 |
| 002-R15 | Cuando el usuario acepte una versión del aviso de privacidad, el sistema deberá registrar versión y fecha. | USR-07 |
| 002-R16 | Mientras una ruta no sea pública, el sistema deberá exigir `Authorization: Bearer` con JWT HS256 válido y no revocado. | Todas (ID 158) |

## 4. Requisitos no funcionales

- Seguridad: OWASP ASVS 2; sin enumeración de cuentas; contraseñas, tokens y correos completos nunca en logs.
- Límites por IP en register, login, refresh, google, check-nickname y endpoints de cuenta (Redis con respaldo local).
- Campos desconocidos → `400`; payload máximo 1 MB; HTTPS obligatorio.
- Rendimiento: login < 500 ms con Argon2id (m=19 MiB, t=2).

## 5. Endpoints y esquemas

USR-01, USR-02, USR-03, USR-05, USR-06, USR-07, AUTH-01 a AUTH-07. Esquemas en `api/openapi.yaml`: `RegisterRequest`, `LoginRequest`, `GoogleAuthRequest`, `RefreshRequest`, `Session`, `User`, `UpdateProfileRequest`, `Profile`, `PublicProfile`, `NicknameAvailability`, `TokenRequest`, `ForgotPasswordRequest`, `ResetPasswordRequest`, `ConsentRequest`, `Error`.

## 6. Criterios de aceptación

| ID | Requisito | Escenario | Evidencia |
|---|---|---|---|
| 002-AC1 | R1, R5, R8, R10 | Registro, login, refresh y logout de punta a punta | `CoreFlowTests`, `FunctionalFlowTests` |
| 002-AC2 | R3 | Google: sin apodo → 422; con apodo → 201; segunda vez → 200; token falso → 401 | `GoogleAuthTests` |
| 002-AC3 | R4 | Sin Client ID → 503 | `UnitTest/GoogleTokenValidatorTests` |
| 002-AC4 | R2, R13 | Apodo duplicado → 409; apodo moderado → 422 | `SocialFlowTests.Profile_GetUpdateAndCheckNickname` |
| 002-AC5 | R16 | Protegidos sin token → 401 | `SecurityTest/AuthorizationTests` |
| 002-AC6 | R6 | Correo inexistente: 401 genérico y una verificación de hash | `SddDecisionTests.Q020_…` |
| 002-AC7 | R7 | 5 fallos → 429 con la contraseña correcta | `SddDecisionTests.Login_AfterFiveFailures_IsTemporarilyLocked` |
| 002-AC8 | R9 | Refresh rotado reutilizado revoca todo; tras cambiar contraseña la sesión nueva sigue viva | `DocComplianceTests`, `AccountFlowTests` |
| 002-AC9 | R11, R12 | Verificación y recuperación con token de un solo uso | `AccountFlowTests` |
| 002-AC10 | R14 | check-nickname público | `SocialFlowTests` |
| 002-AC11 | R15 | Consentimiento con versión vigente | `AccountFlowTests` |

## 7. Preguntas abiertas

Ninguna. Q-002, Q-003, Q-008, Q-015, Q-019 y Q-020 están resueltas en [open-questions](../open-questions.md).

## 8. Verificación contra la constitución

| Regla | Cumple |
|---|---|
| §6.1 sesión, refresh rotativo, reutilización, logout, Argon2id 12–128, bloqueo tras 5, recuperación 1 h | Sí |
| §6.1 verificación obligatoria para grupos | Sí, bandera `Auth:RequireVerifiedEmail` (en producción se activa al configurar SMTP) |
| §6.1 eliminación de cuenta | Fuera de alcance (sprint 3, USR-04) |
| §8 JWT con algoritmo fijado, issuer, audience; solo hash de refresh en BD; rate limit con respaldo local | Sí |
| §3.12 sin requisitos inventados | Sí |

## 9. Plan de pruebas

[Plan de pruebas](../../docs/plan-de-pruebas.md), sección Cuentas.

## 10. Riesgos y deuda

- Envío real de correo pendiente de credencial SMTP; con la bandera apagada la verificación no bloquea grupos.
- El respaldo local del bloqueo es por instancia: con Redis caído, cada una de las 2 instancias cuenta por separado (máximo 10 intentos en vez de 5).

## Aprobación

| Rol | Persona | Fecha | Resultado |
|---|---|---|---|
| Product Owner | David M. | 2026-11-06 | Aprobada |
| Revisión técnica | Kevin H. | Pendiente | — |
| Comprobabilidad | José G. | Pendiente | — |

Expediente: [plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).
