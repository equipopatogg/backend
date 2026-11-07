# Validación — MEM-01 — POST /api/v1/groups/join

- [x] Requiere `Authorization: Bearer` (401 sin token).
- [x] Verifica pertenencia al grupo y propiedad del recurso (403/404 a ajenos).
- [x] Valida la entrada; campos desconocidos y claves NoSQL peligrosas se rechazan (400/422).
- [x] Errores con `code`, `message` y `request_id`; sin datos sensibles en logs.
- [x] Documentado en `api/openapi.yaml`.
- [x] Pruebas en verde: `SocialFlowTests`, `SocialFlowTests.Join_FullGroup_Returns409`, `SocialFlowTests.cs`, `AuthorizationTests.cs`.
- [x] Criterios de aceptación cubiertos: MEM01-AC1, MEM01-AC2, MEM01-AC3, MEM01-AC4.
- [x] Coherente con la constitución (§3, §7, §8).
- [ ] Revisión técnica (Kevin) y comprobabilidad (José).
