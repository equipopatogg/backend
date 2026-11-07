# Tareas — USR-06 — GET /api/v1/users/check-nickname

[Spec](spec.md) · [plan](plan.md).

- [x] USR-06-T1 — RF1: público (AllowAnonymous), sin caché.
- [x] USR-06-T2 — RF2: valida `^[A-Za-z0-9_.-]{3,24}$`; inválido → 422. El Excel dice 3–20; se usa 3–24 para coincidir con el registro (D-15).
- [x] USR-06-T3 — RF3: respuesta `{nickname, available}`; no revela nada del dueño.
- [x] USR-06-T4 — RF4: límite estricto: 20 consultas por minuto por IP (`RateLimit:NicknameCheck*`) → 429.
- [x] T5 — Pruebas: `SocialFlowTests`, `SocialFlowTests.cs`, `AuthorizationTests.cs`.
- [x] T6 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
