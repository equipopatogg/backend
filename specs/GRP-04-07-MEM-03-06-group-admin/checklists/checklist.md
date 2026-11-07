# Validación — GRP-04..07 y MEM-03..06 — Administración del grupo y miembros

- [x] Requiere `Authorization: Bearer` (401 sin token).
- [x] Verifica pertenencia al grupo y propiedad del recurso (403/404 a ajenos).
- [x] Valida la entrada; campos desconocidos y claves NoSQL peligrosas se rechazan (400/422).
- [x] Errores con `code`, `message` y `request_id`; sin datos sensibles en logs.
- [x] Documentado en `api/openapi.yaml`.
- [x] Pruebas en verde: `GroupAdminTests.Admin_EditsNameAndSettings_MemberGets403`, `GroupAdminTests.RegenerateAndDisableInviteCode`, `GroupAdminTests.Roles_CreatorCannotBeTouched_PromotedMemberIsNotified`, `GroupAdminTests.KickedMember_ContractsCancelledBondRefunded_CanRejoinWithBalance`.
- [x] Criterios de aceptación cubiertos: ADM-AC1, ADM-AC2, ADM-AC3, ADM-AC4, ADM-AC5, ADM-AC6.
- [x] Coherente con la constitución (§3, §7, §8).
- [ ] Revisión técnica (Kevin) y comprobabilidad (José).
