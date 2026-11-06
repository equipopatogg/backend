# DEV-01..03 y NOT-04/05 — Dispositivos, preferencias y push con FCM

**Fecha:** 2026-11-05 · **Estado:** Implementado y verificado localmente (envío real pendiente de credenciales de Firebase) · **Historias:** ID 143 Avisos push de retos nuevos, ID 69 Aviso de reto recibido, ID 147 Centro de notificaciones

## 1. Endpoints

- `PUT /api/v1/devices/me` `{ "fcm_token", "platform", "app_version?" }` → registra o actualiza (token único; si estaba en otra cuenta se reasigna).
- `DELETE /api/v1/devices/me?fcm_token=` → quita ese dispositivo o todos (al cerrar sesión).
- `GET` / `PATCH /api/v1/devices/me/preferences` `{ retos, votos, torneos, compromisos }`.
- `DELETE /api/v1/notifications/{id}` (NOT-04) → 204; 404 si no es tuyo.

## 2. Requisitos funcionales

- RF1: NOT-05 (interno): cada aviso se guarda en el centro de notificaciones y se encola un push; la petición HTTP no espera a FCM.
- RF2: el push respeta las preferencias por categoría: `contract_*` → retos, `vote_*` → votos, `tournament_*` → torneos, `commitment_*` → compromisos; otros siempre.
- RF3: avisos duplicados (misma `dedupe_key`) no generan un segundo push.
- RF4: FCM HTTP v1 con cuenta de servicio (JWT RS256 → token OAuth2 reutilizado ~55 min); los tokens `UNREGISTERED` se borran.
- RF5: sin `Firebase:ServiceAccountJson` el push queda apagado y la API funciona igual.

## 3. Configuración

`Firebase:ProjectId` y `Firebase:ServiceAccountJson` (secreto en Parameter Store). La app Android usa `google-services.json` del mismo proyecto y el canal `pactogg_default`.

## 4. Criterios de aceptación

`ProfileContractAuditPushTests.Push_RespectsPreferences_AndDevicesCanBeRemoved`, `ProfileContractAuditPushTests.UnregisteredFcmToken_IsDeleted`, `UnitTest/PushPreferencesTests.cs`.
