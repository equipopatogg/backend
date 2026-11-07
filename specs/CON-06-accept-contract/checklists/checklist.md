# Validación — CON-06 — POST /api/v1/contracts/{id}/accept

- [x] Requiere `Authorization: Bearer` (401 sin token).
- [x] Verifica pertenencia al grupo y propiedad del recurso (403/404 a ajenos).
- [x] Valida la entrada; campos desconocidos y claves NoSQL peligrosas se rechazan (400/422).
- [x] Errores con `code`, `message` y `request_id`; sin datos sensibles en logs.
- [x] Documentado en `api/openapi.yaml`.
- [x] Pruebas en verde: `SocialFlowTests`, `SocialFlowTests.Accept_DirectedToAnotherMember_Returns403`, `SocialFlowTests.cs`, `AuthorizationTests.cs`.
- [x] Criterios de aceptación cubiertos: CON06-AC1, CON06-AC2, CON06-AC3, CON06-AC4, CON06-AC5.
- [x] Coherente con la constitución (§3, §7, §8).
- [ ] Revisión técnica (Kevin) y comprobabilidad (José).
