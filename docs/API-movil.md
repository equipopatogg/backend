# API PactoGG — guía para el equipo Móvil

**Actualizado:** 2026-11-04 (v0.6.1: 35 endpoints, 37 rutas contando alias) · **Contrato vinculante:** [`api/openapi.yaml`](../api/openapi.yaml) (ábrelo en <https://editor.swagger.io> → File → Import file).

## 1. Direcciones

| Uso | URL |
|---|---|
| **Base de la API** | `https://52-3-200-211.sslip.io` |
| Salud | `https://52-3-200-211.sslip.io/health` → `Healthy` |
| CDN de imágenes | `https://d3shxplwv8bmvp.cloudfront.net` |

- Ponla en configuración (`BuildConfig.API_BASE_URL` o `local.properties`), **nunca escrita en el código**: si la infraestructura se destruye y se vuelve a crear, la IP y la URL cambian.
- Solo HTTPS (certificado válido de Let's Encrypt); `network_security_config` puede bloquear texto plano.
- Todas las rutas van bajo `/api/v1`. No hay Swagger en producción: usen el `openapi.yaml`.

## 2. Formato

- Peticiones y respuestas JSON en UTF-8, campos en `snake_case`. Campos desconocidos → `400 INVALID_INPUT`.
- Éxito: `{ "data": ..., "meta": { ... } }` (`meta` solo en listas: `has_more`, `next_cursor`).
- Error: `{ "code": "INVALID_CREDENTIALS", "message": "...", "request_id": "...", "details": {...} }`. La app decide por `code`, no por `message`.
- Fechas ISO-8601 UTC (`2026-11-10T23:59:00Z`). IDs: 24 caracteres hex.
- Listas con cursor: `?limit=20&cursor=<next_cursor>` (máximo 50).

## 3. Sesión (siempre viva)

1. `POST /auth/register` o `POST /auth/login` → `access_token` (15 min), `refresh_token`, `expires_in`.
2. Guarden ambos cifrados (EncryptedSharedPreferences / DataStore cifrado).
3. Cada petición protegida: `Authorization: Bearer <access_token>`.
4. Antes de que venza (o al recibir `401`), `POST /auth/refresh` con `{ "refresh_token": "..." }`. **El refresh rota**: guarden el nuevo y descarten el anterior; reutilizar uno viejo da `401`. Un `Authenticator` de OkHttp con mutex lo resuelve.
5. Si `/auth/refresh` da `401` → ir a iniciar sesión.
6. Logout: `POST /auth/logout` con `{ "refresh_token": "..." }` (o `{ "all": true }` para todos los dispositivos) → `204`; luego borrar datos locales.

## 4. Endpoints disponibles hoy

| ID | Método y ruta | Auth | Cuerpo mínimo | Respuesta |
|---|---|---|---|---|
| USR-01 | `POST /api/v1/auth/register` | No | `email`, `password` (12–128), `nickname` (`^[A-Za-z0-9_.-]{3,24}$`), `accept_privacy: true`, `privacy_version: "2026-10-01"`, `device_id?` | `201` sesión |
| AUTH-01 | `POST /api/v1/auth/login` | No | `email`, `password`, `device_id?` | `200` sesión; `401 INVALID_CREDENTIALS`; `429` tras 5 fallos |
| AUTH-02 | `POST /api/v1/auth/google` | No | `id_token` (de Google Sign-In / Credential Manager con el **Client ID web** del backend); la primera vez además `nickname`, `accept_privacy: true`, `privacy_version` | `200` sesión (ya existía) o `201` (creada); `422 NICKNAME_REQUIRED` → muestra la pantalla de apodo y reintenta; `401 INVALID_GOOGLE_TOKEN`; `503` si el backend no tiene Client ID |
| AUTH-03 | `POST /api/v1/auth/refresh` | No | `refresh_token`, `device_id?` | `200` sesión nueva |
| AUTH-04 | `POST /api/v1/auth/logout` | Sí | `refresh_token?`, `all?` | `204` |
| GRP-01 | `POST /api/v1/groups` | Sí | `name` (2–40), `settings?` (`jurors`, `consensus`, `bond_options`) | `201` grupo con `invite_code`, `my_role`, `my_points` |
| CON-01 | `POST /api/v1/groups/{gid}/contracts` (o `POST /api/v1/contracts` con `group_id`) | Sí | `game`, `condition`, `reward` (3–80, sin dinero), `deadline` (futuro), `bond_required` (de `bond_options`), `target_type` `group`\|`member`, `target_id` si es `member` | `201` contrato `open` |
| EVI-02 | `POST /api/v1/contracts/{id}/evidence` | Sí | JSON `{ "type": "link", "url": "https://youtube.com/..." }` **o** `multipart/form-data` con `file` (JPEG/PNG/WebP ≤ 5 MB) | `201`; la imagen se modera antes de guardarse (`422 CONTENT_REJECTED`) |
| TOR-02 | `GET /api/v1/groups/{gid}/tournaments` (o `GET /api/v1/tournaments?group_id=`) | Sí | filtros `status`, `game`, `limit`, `cursor` | `200` lista con bracket |
| USR-02 | `GET /api/v1/users/me` | Sí | — | `200` perfil: `nickname`, `avatar_id`, `stats` (`completed`, `cheated`, `streak`, `trophies`, `word_index`), `groups_count` |
| USR-03 | `PATCH /api/v1/users/me` | Sí | `nickname?`, `avatar_id?` (al menos uno) | `200` perfil; `409 NICKNAME_ALREADY_EXISTS`; `422 CONTENT_REJECTED` si la moderación lo rechaza |
| USR-06 | `GET /api/v1/users/check-nickname?nickname=` | **No** | — | `200 { nickname, available }`; `422` formato; `429` tras 20/min |
| GRP-02 | `GET /api/v1/groups` | Sí | `q?`, `limit`, `cursor` | `200` lista de mis grupos con `my_role` y `my_points` |
| GRP-03 | `GET /api/v1/groups/{id}` | Sí | — | `200` grupo; `403 NOT_GROUP_MEMBER`; `404` |
| MEM-01 | `POST /api/v1/groups/join` | Sí | `code` (6 caracteres, da igual mayúsculas) | `200` grupo (`my_role: member`, `my_points: 100`); `404` código; `409 ALREADY_MEMBER` / `GROUP_FULL` (20) / `GROUP_LIMIT_REACHED` (5); `429` tras 10 intentos en 10 min |
| MEM-02 | `GET /api/v1/groups/{id}/members` | Sí | `q?` (prefijo del apodo), `limit`, `cursor` | `200` lista: `user_id`, `nickname`, `avatar_id`, `role`, `points_balance`, `joined_at` |
| CON-02 | `GET /api/v1/groups/{gid}/contracts` | Sí | `status?`, `mine?=true`, `game?`, `q?`, `limit`, `cursor` | `200` tablero (más reciente primero) |
| CON-03 | `GET /api/v1/contracts/{id}` | Sí | — | `200` reto con `accepted_by`, `evidence_count`, `version`… |
| CON-06 | `POST /api/v1/contracts/{id}/accept` | Sí | sin cuerpo, o `{ "version": n }` | `200` reto `in_progress` + `my_points` (saldo tras la fianza); `402 INSUFFICIENT_POINTS`; `403 NOT_CONTRACT_TARGET`; `409 CANNOT_ACCEPT_OWN_CONTRACT` / `INVALID_CONTRACT_STATE` |
| CON-07 | `POST /api/v1/contracts/{id}/reject` | Sí | sin cuerpo, o `{ "version": n }` | `200` reto `cancelled` con `closed_reason: "rejected"`; solo el retado; `409` si ya no está `open` |
| EVI-03 | `GET /api/v1/contracts/{id}/evidence` | Sí | — | `200` evidencias aprobadas con `url` (CDN para fotos) |
| VOT-01 | `GET /api/v1/me/votes/pending` | Sí | `limit`, `cursor` | `200` retos que me toca votar (`contract`, `voting_ends_at`) |
| VOT-02 | `PUT /api/v1/contracts/{id}/votes` | Sí | `vote` (`legit`\|`cheat`), `comment?` (≤140) | `200 { status, votes_cast, jurors_assigned, needs_admin }`; `403 NOT_JUROR`; `409 ALREADY_VOTED` |
| VOT-03 | `GET /api/v1/contracts/{id}/votes/summary` | Sí | — | `200` cuántos votaron/faltan, `i_am_juror`, `i_voted`, `admin_can_resolve` |
| VOT-04 | `POST /api/v1/contracts/{id}/votes/resolve` | Sí (admin) | `outcome` (`fulfilled`\|`cheating`) | `200` resultado; `403 NOT_GROUP_ADMIN`; `409 VOTING_NOT_FINISHED` |
| VOT-05 | `GET /api/v1/contracts/{id}/result` | Sí | — | `200` resultado con votos revelados y `bond_movement`; `409` si sigue abierto |
| PTS-01 | `GET /api/v1/groups/{gid}/points/me` | Sí | `type?`, `limit`, `cursor` | `200 { balance, movements[] }` |
| PTS-02 | `GET /api/v1/groups/{gid}/points/ranking` | Sí | `limit` | `200` lista con `position`, `nickname`, `points_balance` |
| COM-01 | `GET /api/v1/groups/{gid}/commitments` | Sí | `role` (`debo`\|`me-deben`), `status?`, `limit`, `cursor` | `200` compromisos con `debtor`, `creditor`, `description` |
| COM-03 | `POST /api/v1/commitments/{id}/complete` | Sí (acreedor) | — | `200` `completed`; `409 INVALID_COMMITMENT_STATE` |
| COM-04 | `POST /api/v1/commitments/{id}/request-complete` | Sí (deudor) | — | `200` `completion_requested` |
| NOT-01 | `GET /api/v1/notifications` | Sí | `unread?`, `limit`, `cursor` | `200` avisos; `meta.unread_count` para el globito |
| NOT-02 | `PATCH /api/v1/notifications/{id}/read` | Sí | — | `200` aviso con `read: true` |
| NOT-03 | `POST /api/v1/notifications/read-all` | Sí | — | `200 { updated }` |
| ACH-01 | `GET /api/v1/groups/{gid}/fame` | Sí | `limit`, `cursor` | `200` retos cumplidos con `executor`, `photos[]`, `links[]` |
| AUTH-05 | `POST /api/v1/auth/verify-email` | No | `token` (del deep link `pactogg://auth/verify-email?token=...` o del código del correo) | `200 { email_verified: true }`; `400 INVALID_OR_EXPIRED_TOKEN` |
| AUTH-05 | `POST /api/v1/auth/verify-email/resend` | Sí | — | `202`; `409 EMAIL_ALREADY_VERIFIED`; `429` (3 por hora) |
| AUTH-06 | `POST /api/v1/auth/forgot-password` | No | `email` | **Siempre** `202` (no revela si la cuenta existe) |
| AUTH-07 | `POST /api/v1/auth/reset-password` | No | `token` (deep link `pactogg://auth/reset-password?token=...`), `new_password` (12–128) | `200`; cierra todas las sesiones → manda a iniciar sesión; `400 INVALID_OR_EXPIRED_TOKEN` |
| — | `POST /api/v1/users/me/password` | Sí | `current_password`, `new_password`, `device_id?` | `200` **sesión nueva** (guarda los tokens nuevos); `422 INVALID_CREDENTIALS`; `409 PASSWORD_NOT_SET` (cuenta de Google) |
| USR-05 | `GET /api/v1/users/{id}` | Sí | — | `200` apodo, avatar, `stats` con `word_index` (sin correo); `403 NO_SHARED_GROUP`; `404` |
| USR-07 | `POST /api/v1/users/me/consent` | Sí | `version` | `200 { version, accepted_at }`; `400` si no es la vigente |
| CON-04 | `PATCH /api/v1/contracts/{id}` | Sí (retador) | `version` (obligatoria), `game?`, `condition?`, `reward?`, `deadline?`, `bond_required?` | `200` reto con `version + 1`; `403 NOT_CONTRACT_OWNER`; `409` si ya lo aceptaron o la versión cambió |
| CON-05 | `DELETE /api/v1/contracts/{id}?version=n` | Sí (retador) | — | `204` (queda `cancelled`); `409` si ya lo aceptaron |
| AUD-01 | `GET /api/v1/groups/{gid}/audit` | Sí (admin) | `entity?`, `entity_id?`, `limit`, `cursor` | `200` historial (acción, quién, cuándo, hash) |
| NOT-04 | `DELETE /api/v1/notifications/{id}` | Sí | — | `204`; `404` si no es tuyo |
| DEV-01 | `PUT /api/v1/devices/me` | Sí | `fcm_token`, `platform` (`android`), `app_version?` | `200` dispositivo |
| DEV-02 | `DELETE /api/v1/devices/me?fcm_token=` | Sí | — | `204` (sin `fcm_token` borra todos) |
| DEV-03 | `GET` / `PATCH /api/v1/devices/me/preferences` | Sí | `retos?`, `votos?`, `torneos?`, `compromisos?` (booleanos) | `200` preferencias |

Dominios de enlace permitidos: youtube.com, youtu.be, twitch.tv, clips.twitch.tv, medal.tv, streamable.com (HTTPS obligatorio).

## 5. Ejemplo rápido

```bash
API=https://52-3-200-211.sslip.io
curl -s -X POST $API/api/v1/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"prueba@pactogg.com","password":"PruebaSegura123!"}'
```

## 6. Lo que todavía NO existe

Muro de la Vergüenza, crear torneos y partidas, contrato por voz con IA (falta la clave del modelo), reportes y bloqueos, eliminar cuenta e Índice de Palabra calculado (hoy vale 50 para todos). El push ya está en la API, pero no enviará nada hasta que se configure la cuenta de servicio de Firebase en el servidor; el correo igual, hasta configurar SES. La lista completa (~90 endpoints) está en `CRUD_y_Endpoints_PactoGG.xlsx`. Mientras tanto, simúlenlos en la app con datos falsos detrás del repositorio.

## 7. Reglas que la app debe respetar

- **Push (Firebase):** al iniciar sesión, y en cada `onNewToken` de FCM, llama `PUT /devices/me`. Al cerrar sesión, `DELETE /devices/me?fcm_token=...` **antes** de `/auth/logout`. El push trae `data.type` y, según el caso, `data.contract_id` / `data.group_id` / `data.commitment_id` para abrir la pantalla correcta. Usa el canal de notificaciones `pactogg_default`. El aviso también queda en el centro de notificaciones aunque el usuario apague ese tipo de push.
- **Verificación de correo:** después del registro con correo muestra "Revisa tu correo". Registra los deep links `pactogg://auth/verify-email` y `pactogg://auth/reset-password` y, si no abren, deja pegar el código. Cuando el backend active la verificación obligatoria, crear o unirse a un grupo sin verificar da `403 EMAIL_NOT_VERIFIED`: ofrece "Reenviar correo".

- Límite de peticiones: `429 RATE_LIMITED` (reintentar con espera).
- Los puntos no son dinero: nada de símbolos de moneda en `reward`.
- La primera evidencia pasa el reto a `voting`; máximo 3 evidencias (`409 EVIDENCE_LIMIT_REACHED`).
- No puedes retarte a ti mismo: `422 INVALID_TARGET`.
- Estados de un reto: `open` → `in_progress` (al aceptarlo; se descuenta la fianza) → `voting` (primera evidencia) → `fulfilled` / `cheating`; también `expired` y `cancelled`. Para un reto a todo el grupo, cualquiera menos el retador puede aceptarlo; si es a una persona, solo esa persona acepta o rechaza.
- Muestra el saldo de `my_points` y deshabilita "Aceptar" si es menor que `bond_required` (el backend igual responde `402`).
- **Idempotency-Key obligatoria** en `POST /contracts/{id}/accept`, `PUT /contracts/{id}/votes` y `POST /contracts/{id}/votes/resolve`: genera un UUID por acción del usuario y reúsalo si reintentas (por red caída). Sin cabecera → `400 IDEMPOTENCY_KEY_REQUIRED`; el reintento devuelve la misma respuesta con `Idempotent-Replayed: true`.
- Para lanzar retos el grupo necesita **al menos 3 miembros** (`409 GROUP_TOO_SMALL`). Jurados configurables de 1 a 7; consenso de 0.50 a 1.0.
- **Jurado:** al subir la primera evidencia el reto pasa a `voting` y se eligen jurados al azar (3 por defecto, nunca el retador ni quien cumple). Con la primera votación ya no se aceptan más evidencias. Consenso = 66 % de los votos emitidos con quórum de la mitad de los jurados (con 3 jurados, 2 iguales cierran). Dura 48 h; si no hay quórum se extiende 24 h una vez (`vote_extended`) y luego decide un admin que **no sea parte** del reto (`403 ADMIN_IS_PARTY`) en 48 h; si nadie decide, el reto queda `cancelled` y se devuelve la fianza. En empate decide un admin neutral o, si no hay, se agrega un jurado extra una vez. Votar fuera de plazo → `409 VOTING_CLOSED`.
- **Al cerrar:** `fulfilled` devuelve la fianza **más un premio igual a la fianza** a quien aceptó y crea un compromiso (el retador le debe la recompensa); `cheating` pasa la fianza al retador. Si vence la fecha límite sin evidencia, el reto pasa a `expired` y la fianza va al retador. Refresca `points/me` y `commitments` después de votar.
- **Resultado (`/result`):** los comentarios se muestran **sin** decir qué jurado votó.
- **Avisos:** consulta `GET /notifications?unread=true` al abrir la app (y cada cierto tiempo) para el globito; los tipos vienen en `type` (`contract_received`, `contract_accepted`, `vote_requested`, `vote_expiring`, `vote_extended`, `vote_tie`, `contract_resolved`, `contract_expired`, `contract_cancelled`, `commitment_completion_requested`, `commitment_completed`). Se guardan 30 días. Push nativo todavía no.
- Un refresh token que se reutiliza (ya rotado) cierra **todas** las sesiones: manda al usuario a iniciar sesión.
- Cuerpos JSON de más de 1 MB → `413 PAYLOAD_TOO_LARGE`. Las respuestas vienen comprimidas con gzip si mandas `Accept-Encoding: gzip` (OkHttp lo hace solo).
- Las listas pueden tardar hasta 30–60 s en reflejar cambios de **otras** personas (caché); tus propias acciones se ven al instante.
- Moderación de fotos: se aceptan capturas de juego con armas, sangre o explosiones; se rechazan desnudos, símbolos de odio, contenido perturbador y gore real (`422 CONTENT_REJECTED` con `details.labels`). Muestra un aviso claro y deja elegir otra foto.
