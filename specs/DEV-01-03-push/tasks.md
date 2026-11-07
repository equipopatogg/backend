# Tareas — DEV-01..03 y NOT-04/05 — Dispositivos, preferencias y push con FCM

[Spec](spec.md) · [plan](plan.md).

- [x] DEV-01-T1 — RF1: NOT-05 (interno): cada aviso se guarda en el centro de notificaciones y se encola un push; la petición HTTP no espera a FCM.
- [x] DEV-01-T2 — RF2: el push respeta las preferencias por categoría: `contract_*` → retos, `vote_*` → votos, `tournament_*` → torneos, `commitment_*` → compromisos; otros siempre.
- [x] DEV-01-T3 — RF3: avisos duplicados (misma `dedupe_key`) no generan un segundo push.
- [x] DEV-01-T4 — RF4: FCM HTTP v1 con cuenta de servicio (JWT RS256 → token OAuth2 reutilizado ~55 min); los tokens se borran.
- [x] DEV-01-T5 — RF5: sin `Firebase:ServiceAccountJson` el push queda apagado y la API funciona igual.
- [x] T6 — Pruebas: `ProfileContractAuditPushTests.Push_RespectsPreferences_AndDevicesCanBeRemoved`, `ProfileContractAuditPushTests.UnregisteredFcmToken_IsDeleted`, `PushPreferencesTests.cs`.
- [x] T7 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
