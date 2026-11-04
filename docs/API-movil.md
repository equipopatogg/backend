# API PactoGG — guía para el equipo Móvil

**Actualizado:** 2026-11-04 (v0.5.0: 19 endpoints, 21 rutas contando alias) · **Contrato vinculante:** [`api/openapi.yaml`](../api/openapi.yaml) (ábrelo en <https://editor.swagger.io> → File → Import file).

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

Dominios de enlace permitidos: youtube.com, youtu.be, twitch.tv, clips.twitch.tv, medal.tv, streamable.com (HTTPS obligatorio).

## 5. Ejemplo rápido

```bash
API=https://52-3-200-211.sslip.io
curl -s -X POST $API/api/v1/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"prueba@pactogg.com","password":"PruebaSegura123!"}'
```

## 6. Lo que todavía NO existe

Editar/cancelar retos, jurado y votos, historial de puntos (`/points/me`), compromisos, crear torneos y partidas, notificaciones y dispositivos (FCM), contrato por voz con IA, reportes, eliminar cuenta, login con Google y verificación de correo. La lista completa (~90 endpoints) está en `CRUD_y_Endpoints_PactoGG.xlsx`. Mientras tanto, simúlenlos en la app con datos falsos detrás del repositorio.

## 7. Reglas que la app debe respetar

- Límite de peticiones: `429 RATE_LIMITED` (reintentar con espera).
- Los puntos no son dinero: nada de símbolos de moneda en `reward`.
- La primera evidencia pasa el reto a `voting`; máximo 3 evidencias (`409 EVIDENCE_LIMIT_REACHED`).
- No puedes retarte a ti mismo: `422 INVALID_TARGET`.
- Estados de un reto: `open` → `in_progress` (al aceptarlo; se descuenta la fianza) → `voting` (primera evidencia) → `fulfilled` / `cheating`; también `expired` y `cancelled`. Para un reto a todo el grupo, cualquiera menos el retador puede aceptarlo; si es a una persona, solo esa persona acepta o rechaza.
- Muestra el saldo de `my_points` y deshabilita "Aceptar" si es menor que `bond_required` (el backend igual responde `402`).
- Las listas pueden tardar hasta 30–60 s en reflejar cambios de **otras** personas (caché); tus propias acciones se ven al instante.
- Moderación de fotos: se aceptan capturas de juego con armas, sangre o explosiones; se rechazan desnudos, símbolos de odio, contenido perturbador y gore real (`422 CONTENT_REJECTED` con `details.labels`). Muestra un aviso claro y deja elegir otra foto.
