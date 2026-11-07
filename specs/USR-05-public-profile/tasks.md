# Tareas — USR-05 — Perfil público · USR-07 — Aceptar aviso de privacidad

[Spec](spec.md) · [plan](plan.md).

- [x] USR-05-T1 — RF1: solo si comparten al menos un grupo (`403 NO_SHARED_GROUP`); uno mismo siempre puede verse. 404 si no existe.
- [x] USR-05-T2 — RF2: nunca se devuelve el correo (CLARIFICACION #14).
- [x] USR-05-T3 — RF3: USR-07 solo acepta `Auth:CurrentPrivacyVersion` (400 en otro caso) y queda en auditoría.
- [x] T4 — Pruebas: `ProfileContractAuditPushTests.PublicProfile_OnlyForSharedGroups_AndNeverShowsEmail`, `AccountFlowTests.Consent_OnlyAcceptsCurrentVersion`.
- [x] T5 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
