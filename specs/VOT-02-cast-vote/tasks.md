# Tareas — VOT-02 — PUT /api/v1/contracts/{id}/votes

[Spec](spec.md) · [plan](plan.md).

- [x] VOT-02-T1 — RF1: Jurados: al subir la primera evidencia se eligen al azar tantos como `settings.jurors` (3 por defecto) entre los miembros, nunca el retador ni quien cumple el r
- [x] VOT-02-T2 — RF2: Solo jurados (403 NOT_JUROR), solo en voting (409), un voto por jurado e inmutable (409 ALREADY_VOTED; índice único contract_id+juror_id).
- [x] VOT-02-T3 — RF3: Comentario opcional ≤140, moderado (falla cerrada).
- [x] VOT-02-T4 — RF4: Consenso: un lado gana cuando sus votos / jurados asignados ≥ consenso del grupo (0.66 por defecto). Con 3 jurados bastan 2 votos iguales.
- [x] VOT-02-T5 — RF5: Cierre en una transacción: fulfilled → bond_release (la fianza regresa a quien aceptó), compromiso retador→ejecutor por la recompensa, completed+1 y racha+1; ch
- [x] VOT-02-T6 — RF6: Si todos votan sin consenso → needs_admin=true y aviso a los administradores.
- [x] T7 — Pruebas: `JuryFlowTests.cs`, `AuthorizationTests.cs`, `VotingRulesTests.cs`, `JuryFlowTests.TiedVote_WithoutNeutralAdmin_AssignsOneExtraJuror`, `JuryFlowTests.VotingDeadlines_ExtendOnce_ThenAdminPhase_ThenCancelWithRefund`.
- [x] T8 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
