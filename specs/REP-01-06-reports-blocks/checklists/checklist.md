# Validación — REP-01..06 — Reportes, eliminación de contenido y bloqueos

- [x] Requiere `Authorization: Bearer` (401 sin token).
- [x] Verifica pertenencia al grupo y propiedad del recurso (403/404 a ajenos).
- [x] Valida la entrada; campos desconocidos y claves NoSQL peligrosas se rechazan (400/422).
- [x] Errores con `code`, `message` y `request_id`; sin datos sensibles en logs.
- [x] Documentado en `api/openapi.yaml`.
- [x] Pruebas en verde: `ReportsAndBlocksTests.Report_ListAndResolveByRemovingContent`, `ReportsAndBlocksTests.Report_TargetFromAnotherGroup_Is404_AndUserReportsAreEscalated`, `ReportsAndBlocksTests.AdminRemovesContentDirectly_MemberCannot`, `ReportsAndBlocksTests.Block_StopsDirectedChallenges_UntilUnblocked`.
- [x] Criterios de aceptación cubiertos: REP-AC1, REP-AC2, REP-AC3, REP-AC4, REP-AC5.
- [x] Coherente con la constitución (§3, §7, §8).
- [ ] Revisión técnica (Kevin) y comprobabilidad (José).
