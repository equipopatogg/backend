# Validación — USR-05 — Perfil público · USR-07 — Aceptar aviso de privacidad

- [x] Requiere `Authorization: Bearer` (401 sin token).
- [x] Verifica pertenencia al grupo y propiedad del recurso (403/404 a ajenos).
- [x] Valida la entrada; campos desconocidos y claves NoSQL peligrosas se rechazan (400/422).
- [x] Errores con `code`, `message` y `request_id`; sin datos sensibles en logs.
- [x] Documentado en `api/openapi.yaml`.
- [x] Pruebas en verde: `ProfileContractAuditPushTests.PublicProfile_OnlyForSharedGroups_AndNeverShowsEmail`, `AccountFlowTests.Consent_OnlyAcceptsCurrentVersion`.
- [x] Criterios de aceptación cubiertos: los de la spec.
- [x] Coherente con la constitución (§3, §7, §8).
- [ ] Revisión técnica (Kevin) y comprobabilidad (José).
