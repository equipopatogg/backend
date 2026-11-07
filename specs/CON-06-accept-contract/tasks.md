# Tareas — CON-06 — POST /api/v1/contracts/{id}/accept

[Spec](spec.md) · [plan](plan.md).

- [x] CON-06-T1 — RF1: quién: el retado si target_type=member (otro → 403 NOT_CONTRACT_TARGET); cualquier miembro salvo el retador si es de grupo.
- [x] CON-06-T2 — RF2: el retador no puede aceptar su reto → 409 CANNOT_ACCEPT_OWN_CONTRACT.
- [x] CON-06-T3 — RF3: solo en open y antes del deadline → si no, 409 INVALID_CONTRACT_STATE.
- [x] CON-06-T4 — RF4: cuerpo opcional {version}; si no coincide → 409 (concurrencia optimista).
- [x] CON-06-T5 — RF5: una transacción MongoDB (Atlas replica set): open→in_progress con versión esperada, $inc -fianza en group_members solo si points_balance ≥ fianza (nunca negativ
- [x] CON-06-T6 — RF6: saldo insuficiente → 402 INSUFFICIENT_POINTS sin cambios; conflicto de escritura → 409.
- [x] CON-06-T7 — RF7: respuesta = reto + my_points (saldo restante). Audita `contract.accepted`; invalida cachés de grupo y usuario.
- [x] T8 — Pruebas: `SocialFlowTests`, `SocialFlowTests.Accept_DirectedToAnotherMember_Returns403`, `SocialFlowTests.cs`, `AuthorizationTests.cs`, `JuryFlowTests.Accept_RequiresIdempotencyKey_AndReplaysTheFirstResponse`, `JuryFlowTests.ContractPastDeadline_WithoutEvidence_ExpiresAndForfeitsBond`.
- [x] T9 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
