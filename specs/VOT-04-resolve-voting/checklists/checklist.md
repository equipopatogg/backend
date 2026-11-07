# Validación — VOT-04 — POST /api/v1/contracts/{id}/votes/resolve

- [x] Requiere `Authorization: Bearer` (401 sin token).
- [x] Verifica pertenencia al grupo y propiedad del recurso (403/404 a ajenos).
- [x] Valida la entrada; campos desconocidos y claves NoSQL peligrosas se rechazan (400/422).
- [x] Errores con `code`, `message` y `request_id`; sin datos sensibles en logs.
- [x] Documentado en `api/openapi.yaml`.
- [x] Pruebas en verde: `JuryFlowTests.cs`, `AuthorizationTests.cs`, `JuryFlowTests.TiedVote_IsResolvedOnlyByNeutralAdmin`.
- [x] Criterios de aceptación cubiertos: los de la spec.
- [x] Coherente con la constitución (§3, §7, §8).
- [ ] Revisión técnica (Kevin) y comprobabilidad (José).
