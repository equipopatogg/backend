# 006 — Push, permisos y modo sin conexión

**Fecha:** 2026-11-06 · **Estado:** Parte del backend aprobada e implementada salvo 006-T7..T10; parte móvil por especificar con Diego S. · **Sprint:** 3 (fila 65 del Sprint Planning)
**Responsables:** Diego S. (móvil, dueño de la fila) · David M. (backend).
**Historias:** ID 69 Aviso de reto recibido · ID 143 Avisos push de retos nuevos · ID 147 Centro de notificaciones · permisos de micrófono, notificaciones y cámara · Room (SQLite) y sincronización sin conexión.
**Expedientes:** [DEV-01..03 y NOT-04/05](../DEV-01-03-push/spec.md), [NOT-01](../NOT-01-list-notifications/spec.md), [NOT-02](../NOT-02-read-notification/spec.md), [NOT-03](../NOT-03-read-all/spec.md).
**Fuentes:** constitución §4 (push, 5 dispositivos); CLARIFICACION #116–#128 y #12; Descripción del Backend 7.10; Descripción del Móvil §12; CRUD hojas Sesiones y dispositivos, y Notificaciones.

## 1. Contexto y objetivo

Que cada persona se entere a tiempo de lo que le toca (un reto, un voto, un resultado, un compromiso) con un aviso dentro de la app y un push al celular, que decida qué tipos quiere recibir, y que la app siga siendo útil sin internet mostrando la última copia guardada.

## 2. Fuera de alcance

- **Permisos del sistema** (micrófono, notificaciones, cámara) y su manejo cuando se niegan: solo móvil; Diego S. los especifica en la parte móvil de esta spec.
- **Room y WorkManager:** solo móvil. El backend solo garantiza lo que esas funciones necesitan (sección 3, R12–R14).
- **Votar, aceptar con fianza, subir fotos, reportar resultados de torneo, iniciar sesión o eliminar la cuenta sin conexión:** bloqueado en la app (CLARIFICACION #124).
- **Avisos de torneos y de cambios de grupo:** se emiten cuando existan esos endpoints (TOR-01..09, MEM-03..05); el mecanismo de esta spec ya los admite.
- **Correo como canal de avisos:** solo para seguridad de la cuenta (verificación y recuperación).

## 3. Requisitos funcionales (EARS)

### Dispositivos y preferencias

| ID | Requisito | Endpoint |
|---|---|---|
| 006-R1 | Cuando la app registre un token de FCM, el sistema deberá guardarlo o actualizarlo; si el token pertenecía a otra cuenta (otro usuario en el mismo celular), deberá reasignarlo. | DEV-01 |
| 006-R2 | Si una cuenta registra un sexto dispositivo, entonces el sistema deberá quitar el que lleva más tiempo sin actualizarse, para dejar 5 (006-D1). | DEV-01 |
| 006-R3 | Cuando la persona cierre sesión en ese celular, la app deberá llamar DEV-02 con su token; cuando cierre sesión en todos lados, restablezca la contraseña o se detecte un refresh robado, el sistema deberá quitar todos sus dispositivos. | DEV-02, AUTH-04, AUTH-07 |
| 006-R4 | Cuando la persona cambie sus preferencias, el sistema deberá guardar por separado retos, votos, torneos y compromisos. | DEV-03 |

### Avisos

| ID | Requisito | Endpoint |
|---|---|---|
| 006-R5 | Cuando ocurra un reto recibido, un voto pendiente, una votación por vencer, un resultado o un compromiso marcado o por confirmar, el sistema deberá crear el aviso en el centro de notificaciones y encolar el push sin hacer esperar a la petición. | NOT-05 (interno) |
| 006-R6 | Si un aviso tiene la misma clave de usuario, tipo, referencia y evento que uno existente, entonces el sistema no deberá crear otro ni enviar otro push. | NOT-05 |
| 006-R7 | Mientras una categoría esté desactivada, el sistema no deberá enviar push de esa categoría (`contract_*` retos, `vote_*` votos, `tournament_*` torneos, `commitment_*` compromisos), pero sí guardar el aviso; los avisos de seguridad no se pueden desactivar. | NOT-05 |
| 006-R8 | Cuando FCM responda que un token ya no existe, el sistema deberá borrarlo. | NOT-05 |
| 006-R9 | Si no hay cuenta de servicio de Firebase configurada, entonces el sistema deberá dejar el push apagado y seguir guardando avisos. | NOT-05 |
| 006-R10 | Cuando la persona abra el centro, el sistema deberá listar sus avisos (más nuevos primero, filtro de no leídos, cursor) y permitir marcar uno, marcar todos y borrar uno; solo los suyos (404 para ajenos). | NOT-01..04 |
| 006-R11 | Cuando un aviso cumpla 30 días, el sistema deberá eliminarlo. | Índice TTL |
| 006-R15 | Cuando un compromiso lleve más de 7 días pendiente, el sistema deberá avisar al deudor una vez ("compromiso por cumplir"). | NOT-05 |

### Apoyo al modo sin conexión

| ID | Requisito | Endpoint |
|---|---|---|
| 006-R12 | Mientras la app trabaje con copias locales, cada recurso del servidor deberá traer `version` y `updated_at` para que gane el servidor y la app muestre qué cambió (CLARIFICACION #125). | CON-02/03, GRP-03, MEM-02, COM-01, USR-02 |
| 006-R13 | Cuando la app reenvíe un borrador guardado sin conexión (crear reto o registrar un enlace de evidencia) con una `Idempotency-Key`, el sistema deberá devolver la primera respuesta en los reintentos en vez de crear un duplicado. | CON-01, EVI-02 |
| 006-R14 | Si una acción guardada ya no es válida al volver internet (por ejemplo, el reto venció), entonces el sistema deberá responder con su código de error normal (`409`, `422`) para que la app la descarte y avise con el motivo. | Todas |

## 3b. Parte móvil (borrador para Diego S.)

| ID | Requisito |
|---|---|
| 006-M1 | Cuando la persona vaya a dictar, la app deberá pedir el permiso de micrófono en ese momento, explicando para qué; si lo niega, ofrecer escribir el reto a mano. |
| 006-M2 | Cuando la persona inicie sesión en Android 13 o más, la app deberá pedir el permiso de notificaciones con una pantalla previa; si lo niega, el centro de avisos sigue funcionando. |
| 006-M3 | Cuando la persona suba una foto, la app deberá usar el selector o la cámara del sistema (sin CameraX), sin pedir permiso de almacenamiento. |
| 006-M4 | Mientras no haya conexión, la app deberá mostrar desde Room la última copia del tablero del grupo activo, compromisos, miembros y perfil, con una marca de "sin conexión". |
| 006-M5 | Mientras no haya conexión, la app deberá permitir guardar borradores de retos y enlaces de evidencia, y bloquear iniciar sesión, aceptar con fianza, votar, subir fotos, reportar resultados de torneo y eliminar la cuenta. |
| 006-M6 | Cuando vuelva la conexión, WorkManager deberá enviar los borradores con `Idempotency-Key` y hasta 5 reintentos (30 s, 1, 2, 5 y 15 min); si el servidor rechaza una acción, la app la descarta y avisa con el motivo (marca en el elemento, aviso y mensaje al abrir). |
| 006-M7 | Cuando la versión del servidor sea distinta a la copia local, la app deberá quedarse con la del servidor y mostrar qué cambió. |
| 006-M8 | Al iniciar sesión, la app deberá registrar el token de FCM (DEV-01); al cerrar sesión, quitarlo (DEV-02) y borrar la copia local. |

## 4. Requisitos no funcionales

- FCM HTTP v1 sin SDK: token OAuth2 reutilizado ~55 minutos; cola acotada en memoria; la petición HTTP no espera al push.
- La cuenta de servicio de Firebase vive en SSM (`Firebase__ServiceAccountJson`), nunca en el repositorio ni en el APK.
- El contenido del push no lleva correos ni datos sensibles; solo título, cuerpo corto e ids para abrir la pantalla.
- Reintentos de la app sin conexión: 5 con espera creciente (30 s, 1, 2, 5 y 15 min); el backend los soporta gracias a R13 y a los límites por usuario.

## 5. Endpoints y esquemas

DEV-01 `PUT /devices/me`, DEV-02 `DELETE /devices/me`, DEV-03 `GET`/`PATCH /devices/me/preferences`, NOT-01 `GET /notifications`, NOT-02 `PATCH /notifications/{id}/read`, NOT-03 `POST /notifications/read-all`, NOT-04 `DELETE /notifications/{id}`, NOT-05 interno. Esquemas en `api/openapi.yaml`: `RegisterDeviceRequest`, `Device`, `NotificationPreferences`, `Notification`, `ReadAll`.

## 6. Criterios de aceptación

| ID | Requisito | Escenario | Evidencia |
|---|---|---|---|
| 006-AC1 | R1, R4, R7 | Push respeta preferencias; el dispositivo se puede quitar | `ProfileContractAuditPushTests.Push_RespectsPreferences_AndDevicesCanBeRemoved`, `UnitTest/PushPreferencesTests` |
| 006-AC2 | R8 | Token `UNREGISTERED` se borra | `ProfileContractAuditPushTests.UnregisteredFcmToken_IsDeleted` |
| 006-AC3 | R2 | Sexto dispositivo → quedan 5 y sale el más viejo | `Spec006PushTests.SixthDevice_ReplacesTheLeastRecentlyUsed` |
| 006-AC4 | R3 | Logout en todos lados → sin dispositivos | `Spec006PushTests.LogoutEverywhere_RemovesAllDevices` |
| 006-AC5 | R5 | Reto dirigido → aviso solo al retado | `Spec004ContractTests.DirectedContract_NotifiesOnlyTheTarget` |
| 006-AC6 | R6 | El aviso de votación por vencer llega una sola vez aunque la tarea corra dos veces | `JuryFlowTests` (cuenta de `vote_expiring` = 1) |
| 006-AC7 | R9 | Sin Firebase la API funciona y guarda avisos | Todas las suites corren sin Firebase |
| 006-AC8 | R10 | Listar, leer, leer todos, borrar; ajeno → 404 | `JuryFlowTests.FulfilledContract_…` (NOT-01..03), `ProfileContractAuditPushTests` (NOT-04) |
| 006-AC9 | R11 | Índice `ttl_notifications_30d` | `MongoIndexInitializer` y `database/mongo-init.js` |
| 006-AC10 | R12 | Respuestas con `version` y `updated_at` | Cumple `Contract`; faltan `Group`, `GroupMember`, `Commitment` y `Profile` (006-T10) |
| 006-AC11 | R13 | Reintento de CON-01 con la misma clave → un solo reto | Pendiente (006-T8) |
| 006-AC12 | R14 | Aceptar un reto vencido → 409 | `JuryFlowTests.ContractPastDeadline_…` |
| 006-AC13 | R15 | Compromiso de 8 días → un aviso al deudor | Pendiente (006-T9) |

## 7. Preguntas abiertas

Ninguna para la parte del backend. La parte móvil la completa Diego S. con el mismo formato.

## 8. Verificación contra la constitución

| Regla | Cumple |
|---|---|
| §4 FCM y hasta 5 dispositivos por cuenta | Sí (R1, R2) |
| §6.1 eliminar cuenta borra dispositivos | Pendiente con USR-04 (sprint 3) |
| §3.7 secretos fuera del repositorio | Sí |
| §8 logs sin PII | Sí, el push no registra contenido |
| CLARIFICACION #117–#121 | Sí (R3–R7) |

## 9. Decisiones de esta spec

- **006-D1:** al pasar de 5 dispositivos se quita el menos usado en vez de rechazar el nuevo, para que la persona nunca se quede sin push en su celular actual. Aprobada por el PO el 2026-11-06.

## 10. Riesgos y deuda

- El push real depende de la cuenta de servicio de Firebase en producción (se configura con SMTP en el mismo cambio de `user_data`).
- La cola de push es en memoria por instancia: si la instancia se reinicia se pierden los push pendientes (el aviso queda guardado en el centro).

## Aprobación

| Rol | Persona | Fecha | Resultado |
|---|---|---|---|
| Product Owner | David M. | 2026-11-06 | Aprobada |
| Móvil | Diego S. | — | Por completar parte móvil |
| Revisión técnica | Kevin H. | Pendiente | — |
| Comprobabilidad | José G. | Pendiente | — |

Expediente: [plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).
