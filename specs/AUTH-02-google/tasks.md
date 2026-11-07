# Tareas — AUTH-02 — POST /api/v1/auth/google

[Spec](spec.md) · [plan](plan.md).

- [x] AUTH-02-T1 — RF1: público, con límite de peticiones de login (10/min por IP).
- [x] AUTH-02-T2 — RF2: `id_token` validado con las llaves públicas de Google (descubrimiento OpenID en caché): firma, emisor `accounts.google.com`, audiencia en `Auth:GoogleClientIds`
- [x] AUTH-02-T3 — RF3: si existe cuenta con ese `google_sub` → 200; si existe con el mismo correo → se vincula (`google_sub`, `email_verified=true`) y 200.
- [x] AUTH-02-T4 — RF4: cuenta nueva: exige `nickname` (si falta, `422 NICKNAME_REQUIRED` con `details.email` y `details.suggested_name`), `accept_privacy=true` y `privacy_version` vig
- [x] AUTH-02-T5 — RF5: sin Client IDs configurados → `503 GOOGLE_AUTH_UNAVAILABLE` (falla cerrada); Google caído → 503.
- [x] AUTH-02-T6 — RF6: auditoría `auth.login_google`, `auth.google_linked`, `user.registered_google`.
- [x] T7 — Pruebas: `GoogleAuthTests.cs`, `GoogleTokenValidatorTests.cs`.
- [x] T8 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
