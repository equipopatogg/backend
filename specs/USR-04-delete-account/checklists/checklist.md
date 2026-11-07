# Validación — USR-04 — DELETE /api/v1/users/me (eliminar cuenta)

- [x] Requiere `Authorization: Bearer` (401 sin token).
- [x] Verifica pertenencia al grupo y propiedad del recurso (403/404 a ajenos).
- [x] Valida la entrada; campos desconocidos y claves NoSQL peligrosas se rechazan (400/422).
- [x] Errores con `code`, `message` y `request_id`; sin datos sensibles en logs.
- [x] Documentado en `api/openapi.yaml`.
- [x] Pruebas en verde: `DeleteAccountTests.WrongPasswordOrMissingConfirmation_Is422`, `DeleteAccountTests.Delete_AnonymizesRevokesAndHandsOverOwnedGroup`, `DeleteAccountTests.SoleMember_GroupIsDeleted`.
- [x] Criterios de aceptación cubiertos: USR04-AC1, USR04-AC2, USR04-AC3.
- [x] Coherente con la constitución (§3, §7, §8).
- [ ] Revisión técnica (Kevin) y comprobabilidad (José).
