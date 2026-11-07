# Validación — USR-03 — PATCH /api/v1/users/me

- [x] Requiere `Authorization: Bearer` (401 sin token).
- [x] Verifica pertenencia al grupo y propiedad del recurso (403/404 a ajenos).
- [x] Valida la entrada; campos desconocidos y claves NoSQL peligrosas se rechazan (400/422).
- [x] Errores con `code`, `message` y `request_id`; sin datos sensibles en logs.
- [x] Documentado en `api/openapi.yaml`.
- [x] Pruebas en verde: `SocialFlowTests`, `SocialFlowTests.cs`, `AuthorizationTests.cs`.
- [x] Criterios de aceptación cubiertos: USR03-AC1, USR03-AC2, USR03-AC3, USR03-AC4.
- [x] Coherente con la constitución (§3, §7, §8).
- [ ] Revisión técnica (Kevin) y comprobabilidad (José).
