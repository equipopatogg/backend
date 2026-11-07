# Tareas — CON-07 — POST /api/v1/contracts/{id}/reject

[Spec](spec.md) · [plan](plan.md).

- [x] CON-07-T1 — RF1: solo target_type=member y target_id = usuario (otro → 403 NOT_CONTRACT_TARGET).
- [x] CON-07-T2 — RF2: solo en open; versión opcional como en CON-06 → 409.
- [x] CON-07-T3 — RF3: pasa a cancelled con closed_reason=rejected y rejected_by (la constitución no tiene estado rejected; open→cancelled es transición válida).
- [x] CON-07-T4 — RF4: no mueve puntos (la fianza solo se retiene al aceptar).
- [x] CON-07-T5 — RF5: audita `contract.rejected` e invalida la caché del grupo.
- [x] T6 — Pruebas: `SocialFlowTests`, `SocialFlowTests.cs`, `AuthorizationTests.cs`.
- [x] T7 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
