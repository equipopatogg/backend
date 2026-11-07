# Validación — USR-06 — GET /api/v1/users/check-nickname

- [x] Pública con límite de peticiones por IP (sin datos de otras cuentas en la respuesta).
- [x] Respuesta genérica: no revela si una cuenta existe.
- [x] Valida la entrada; campos desconocidos y claves NoSQL peligrosas se rechazan (400/422).
- [x] Errores con `code`, `message` y `request_id`; sin datos sensibles en logs.
- [x] Documentado en `api/openapi.yaml`.
- [x] Pruebas en verde: `SocialFlowTests`, `SocialFlowTests.cs`, `AuthorizationTests.cs`.
- [x] Criterios de aceptación cubiertos: USR06-AC1, USR06-AC2.
- [x] Coherente con la constitución (§3, §7, §8).
- [ ] Revisión técnica (Kevin) y comprobabilidad (José).
