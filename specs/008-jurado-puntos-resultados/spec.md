# 008 — Jurado, puntos y resultados

**Fecha:** 2026-11-07 · **Estado:** Aprobada e implementada (v0.10.0, 2026-11-10); el trofeo `campeon_torneo` se entrega con la spec 010 · **Sprint:** 4 (fila 100)
**Historias:** jurado aleatorio, votación y resultado, libro de puntos y fianzas, ranking y Rey del Grupo, Muro de la Fama y de la Vergüenza, logros, Índice de Palabra (ID 25).
**Endpoints del CRUD:** VOT-01..05, PTS-01..03, ACH-01, ACH-03 (hechos); ACH-02, ACH-04 (por hacer); PTS-04/05 internos; ACH-05/06 fuera de alcance.
**Fuentes:** constitución §6.5, §6.6, §6.8; CLARIFICACION #71–#95, #108–#115; Descripción del Backend 7.6, 7.7, 7.9; CRUD hojas Votos, Puntos y Logros; F2-D40–F2-D44.

## 1. Contexto y objetivo

Cuando hay evidencia, un jurado del grupo decide si el reto se cumplió o fue trampa. El resultado mueve puntos solo por el libro inmutable, alimenta el ranking, los muros y los logros, y recalcula el Índice de Palabra de cada persona.

## 2. Fuera de alcance

- Temporadas (ACH-05/06, CLARIFICACION #113): un solo ranking sin temporadas.
- Ajustes manuales de puntos por el administrador (CLARIFICACION #93): el CRUD no les da endpoint (PTS-04 es interno).
- Apelación del Índice (CLARIFICACION #111).

## 3. Requisitos funcionales (EARS)

| ID | Requisito | Endpoint |
|---|---|---|
| 008-R1 | Cuando un reto pase a `voting`, el sistema deberá elegir al azar `settings.jurors` jurados (1–7, 3 por defecto) entre miembros activos, nunca el retador ni el retado, y avisarles. | Interno |
| 008-R2 | Cuando un jurado vote `legit` o `cheat` con `Idempotency-Key`, el sistema deberá guardar un voto inmutable por jurado con comentario opcional de hasta 140 caracteres moderado. | VOT-02 |
| 008-R3 | Cuando haya quórum (la mitad de los jurados asignados) y un lado tenga ≥ 66 % de los votos emitidos sin poder cambiar, el sistema deberá cerrar: `fulfilled` libera la fianza más un premio igual y crea el compromiso; `cheating` entrega la fianza al retador. | VOT-02 |
| 008-R4 | Si vencen las 48 h sin consenso, el sistema deberá extender 24 h una vez; luego pasar a decisión del administrador; si no decide en 48 h, `cancelled` con devolución de la fianza y sin cambio de reputación. | Tarea programada |
| 008-R5 | Si hay empate, entonces decide un administrador neutral (VOT-04) o, si no lo hay, se asigna un jurado extra una sola vez. | VOT-04 |
| 008-R6 | Mientras la votación esté abierta, el sistema no deberá revelar quién votó qué; al cerrar mostrará votos y comentarios sin autor. | VOT-03, VOT-05 |
| 008-R7 | Cuando cambie un saldo, el sistema deberá hacerlo solo con movimientos del libro (`bond_lock`, `bond_release`, `bond_forfeit`, `reward`, …) dentro de una transacción; el saldo nunca es negativo y los movimientos no se editan. | PTS-01..03 |
| 008-R8 | Cuando un miembro pida el ranking, el sistema deberá ordenar por puntos y marcar al Rey del Grupo (primer lugar sin empate). | ACH-03 |
| 008-R9 | Cuando un miembro pida el Muro de la Vergüenza, el sistema deberá listar los retos del grupo que terminaron en `cheating` o `expired`, más nuevos primero, con cursor y en tono ligero (sin fotos rechazadas ni comentarios). | ACH-02 |
| 008-R10 | Cuando se cierre un reto o un torneo, el sistema deberá otorgar los logros del catálogo v1 (008-D1) una sola vez por persona y fuente. | Interno |
| 008-R11 | Cuando alguien que comparte grupo pida los logros de una persona, el sistema deberá devolverlos (tipo, código, nombre, fecha, fuente); a quien no comparte grupo, `404`. | ACH-04 |
| 008-R12 | Cuando ocurra un evento que cuenta para el Índice (reto cumplido, trampa confirmada, vencido sin evidencia, compromiso cumplido a tiempo, compromiso vencido > 7 días, voto alineado con el consenso), el sistema deberá recalcular de forma determinista `índice = clamp(0, 100, 50 + 4·cumplidos − 12·trampas − 5·vencidos + 2·compromisos_a_tiempo − 4·compromisos_vencidos + min(10, votos_alineados))` y guardarlo en el perfil. | USR-02, USR-05 |

## 4. Requisitos no funcionales

Transacciones MongoDB en cierre y fianzas; índice único `contract_id + juror_id`; tarea programada cada 5 minutos; ranking en caché 60 s; el Índice se recalcula desde contadores, no desde texto.

## 5. Endpoints y esquemas

VOT-01..05, PTS-01..03, ACH-01..04 (rutas en la lista de endpoints). Esquemas: `Vote`, `VoteSummary`, `ContractResult`, `PointsMovement`, `GroupRanking`, `ShameItem`, `Achievement`.

## 6. Criterios de aceptación

| ID | Requisito | Evidencia |
|---|---|---|
| 008-AC1 | R1–R3 | `JuryFlowTests.FulfilledContract_…`, `VotingRulesTests` |
| 008-AC2 | R4 | `JuryFlowTests.VotingDeadlines_ExtendOnce_ThenAdminPhase_ThenCancelWithRefund` |
| 008-AC3 | R5 | `JuryFlowTests.TiedVote_WithoutNeutralAdmin_AssignsOneExtraJuror` |
| 008-AC4 | R6 | `JuryFlowTests` (resumen sin autores) |
| 008-AC5 | R7 | `JuryFlowTests` (libro y saldo), `DetailEndpointsTests` |
| 008-AC6 | R8 | `DetailEndpointsTests.PointsMovement_OnlyOwnerOrAdmin_AndRankingHasKing` |
| 008-AC7 | R9 | `Spec008ReputationTests.AC7_ShameWall_ListsCheatingAndExpired_NotFulfilled_OnlyForMembers` |
| 008-AC8 | R10, R11 | `Spec008ReputationTests.AC8_AC9_FulfilledContract_…`, `AC8_Streaks_AreAwardedOncePerPerson` |
| 008-AC9 | R12 | `Spec008ReputationTests.AC9_WordIndexFormula_IsDeterministicAndClamped`, `AC9_AcceptedContractThatExpires_…`, `AC8_AC9_FulfilledContract_…`, `Spec006And009Tests.AC13_…` |

## 7. Decisiones (aprobadas por el PO el 2026-11-07)

- **008-D1 — catálogo v1 de logros** (solo los ejemplos de CLARIFICACION #115 y los tipos del CRUD): insignia `primer_reto` (primer reto cumplido), rachas `racha_3`, `racha_5` y `racha_10` (retos cumplidos seguidos), trofeo `campeon_torneo` (grupo campeón; a cada miembro que jugó). Más logros se agregan por enmienda de esta spec.
- **008-D2:** el Índice usa la fórmula propuesta de la constitución §6.8 mientras el equipo no ratifique otra.

## 8. Preguntas abiertas

Ninguna.

## 9. Verificación contra la constitución

§6.5 jurado, consenso, plazos y empate ✔ · §6.6 libro inmutable y saldo no negativo ✔ · §6.8 Índice determinista ✔ (008-T1) · §7.1 `Idempotency-Key` al votar ✔.

## Aprobación

| Rol | Persona | Fecha | Resultado |
|---|---|---|---|
| Product Owner | David M. | 2026-11-07 | Aprobada |
| Revisión técnica | Kevin H. | — | Pendiente |
| Comprobabilidad | José G. | — | Pendiente |

Expediente: [plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).
