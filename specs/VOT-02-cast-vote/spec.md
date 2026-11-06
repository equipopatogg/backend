# VOT-02 — PUT /api/v1/contracts/{id}/votes

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas en verde).
**Contrato:** `api/openapi.yaml` (v0.6.0)

## 1. Objetivo

ID 88 · Votación legítimo o trampa; ID 99/100 · entrega o pérdida de fianza.

## 2. Requisitos funcionales

- RF1: Jurados: al subir la primera evidencia se eligen al azar tantos como `settings.jurors` (3 por defecto) entre los miembros, nunca el retador ni quien cumple el reto; 48 h para votar; se les avisa.
- RF2: Solo jurados (403 NOT_JUROR), solo en voting (409), un voto por jurado e inmutable (409 ALREADY_VOTED; índice único contract_id+juror_id).
- RF3: Comentario opcional ≤140, moderado (falla cerrada).
- RF4: Consenso: un lado gana cuando sus votos / jurados asignados ≥ consenso del grupo (0.66 por defecto). Con 3 jurados bastan 2 votos iguales.
- RF5: Cierre en una transacción: fulfilled → bond_release (la fianza regresa a quien aceptó), compromiso retador→ejecutor por la recompensa, completed+1 y racha+1; cheating → bond_forfeit (la fianza pasa al retador), cheated+1 y racha a 0.
- RF6: Si todos votan sin consenso → needs_admin=true y aviso a los administradores.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_JUROR / NOT_GROUP_MEMBER` | No es jurado |
| 409 | `ALREADY_VOTED / INVALID_CONTRACT_STATE` | Ya votó o no está en voting |
| 422 | `VALIDATION_ERROR / CONTENT_REJECTED` | vote inválido o comentario moderado |
| 503 | `MODERATION_UNAVAILABLE` | Moderación caída |

## 4. Implementación

`Pactogg.Api/Modules/Votes/VoteService.cs, MongoContractRepository (TryRegisterVoteAsync, ResolveAsync)`

## 5. Criterios de aceptación

| Escenario | Evidencia |
|---|---|
| 2 de 3 legit → fulfilled, saldo 100, compromiso creado | IntegrationTest/JuryFlowTests.cs |
| 1 de 1 cheat → cheating, retador 125, ejecutor 75 | IntegrationTest/JuryFlowTests.cs |
| Votar dos veces → 409; no jurado → 403 | IntegrationTest/JuryFlowTests.cs |

401 sin token: `SecurityTest/AuthorizationTests.cs`.

## Cambio 0.7.0 (alineación con CLARIFICACION #79–83 y DESC_BACK 7.6)

- RF-A: consenso = votos del lado × 100 ≥ consenso × votos **emitidos**, con quórum de la mitad de los jurados asignados. Cierre anticipado solo si ya no puede cambiar.
- RF-B: a las 48 h sin quórum se extiende 24 h una sola vez (aviso `vote_extended`); 6 h antes del cierre se avisa `vote_expiring`.
- RF-C: empate → admin que no sea parte; si no hay, un jurado adicional una sola vez.
- RF-D: fuera de plazo o en fase del administrador → `409 VOTING_CLOSED`. Con el primer voto ya no se aceptan evidencias.
- RF-E: cabecera `Idempotency-Key` obligatoria.
- Pruebas: `UnitTest/VotingRulesTests.cs`, `JuryFlowTests.TiedVote_WithoutNeutralAdmin_AssignsOneExtraJuror`, `JuryFlowTests.VotingDeadlines_ExtendOnce_ThenAdminPhase_ThenCancelWithRefund`.
