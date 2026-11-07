# Validación — USR-02 — GET /api/v1/users/me

- [x] Requiere `Authorization: Bearer` (401 sin token).
- [x] Verifica pertenencia al grupo y propiedad del recurso (403/404 a ajenos).
- [x] Valida la entrada; campos desconocidos y claves NoSQL peligrosas se rechazan (400/422).
- [x] Errores con `code`, `message` y `request_id`; sin datos sensibles en logs.
- [x] Documentado en `api/openapi.yaml`.
- [x] Pruebas en verde: `SocialFlowTests.Profile_GetUpdateAndCheckNickname`, `SocialFlowTests`, `AuthorizationTests`, `SocialFlowTests.cs`.
- [x] Criterios de aceptación cubiertos: USR02-AC1, USR02-AC2, USR02-AC3.
- [x] Coherente con la constitución (§3, §7, §8).
- [ ] Revisión técnica (Kevin) y comprobabilidad (José).
