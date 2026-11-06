# USR-05 — Perfil público · USR-07 — Aceptar aviso de privacidad

**Fecha:** 2026-11-05 · **Estado:** Implementado y verificado localmente · **Historias:** ID 23/25 (perfil e Índice de Palabra), ID 19 (privacidad)

## 1. Endpoints

- `GET /api/v1/users/{id}` → apodo, avatar, estadísticas (incluye `word_index`) y fecha de alta. Caché 60 s.
- `POST /api/v1/users/me/consent` `{ "version" }` → registra la versión vigente.

## 2. Requisitos funcionales

- RF1: solo si comparten al menos un grupo (`403 NO_SHARED_GROUP`); uno mismo siempre puede verse. 404 si no existe.
- RF2: nunca se devuelve el correo (CLARIFICACION #14).
- RF3: USR-07 solo acepta `Auth:CurrentPrivacyVersion` (400 en otro caso) y queda en auditoría.

## 3. Criterios de aceptación

`ProfileContractAuditPushTests.PublicProfile_OnlyForSharedGroups_AndNeverShowsEmail`, `AccountFlowTests.Consent_OnlyAcceptsCurrentVersion`.
