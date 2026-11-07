# 004 — Contratos y demo

**Fecha:** 2026-11-06 · **Estado:** Aprobada; implementada salvo 004-T9 (demo en la app) · **Sprint:** 3 (fila 63 del Sprint Planning)
**Historias:** ID 42 Tablero de contratos · ID 43 Publicar reto · ID 44 Destinatarios de un reto · ID 70 Aceptar un reto · ID 160 Historial inmutable de contratos · ID 1 Demo interactiva · ID 5 Acceso al registro desde la demo.
**Expedientes:** [CON-01](../CON-01-create-contract/spec.md), [CON-02](../CON-02-list-contracts/spec.md), [CON-03](../CON-03-get-contract/spec.md), [CON-04/05](../CON-04-05-edit-cancel/spec.md), [CON-06](../CON-06-accept-contract/spec.md), [CON-07](../CON-07-reject-contract/spec.md), [AUD-01](../AUD-01-group-audit/spec.md).
**Fuentes:** constitución §6.3; CLARIFICACION #30–#40, #1 y #130; Descripción del Backend 7.3; Descripción del Móvil 7.1; CRUD hoja Contratos.

## 1. Contexto y objetivo

Que un miembro publique un reto (juego, condición, recompensa, fecha límite y fianza) para una persona o para todo el grupo, que el grupo lo vea en su tablero, que el retado lo acepte reteniendo la fianza o lo rechace, y que cada cambio de estado quede en el historial inmutable. La demo previa al registro enseña ese flujo sin cuenta.

## 2. Fuera de alcance

- **Demo (ID 1, 2, 5):** corre solo en la app, con datos de ejemplo y sin llamadas al backend (Descripción del Móvil 7.1: "No pide datos"). No hay endpoint de demo ni acceso anónimo a la IA.
- **Contrato por voz (CON-08):** spec [005](../005-contrato-por-voz/spec.md).
- **Evidencias, jurado, votación y resultado:** specs 007 y 008. Aquí solo se fija que la primera evidencia pasa el reto a `voting`.
- **Contratos con varios participantes** (CLARIFICACION #40).
- **Cancelación de un reto aceptado por decisión del administrador** (CLARIFICACION #35): el CRUD no le asigna endpoint; registrada como Q-022.
- **Cancelar los retos del creador que sale o es expulsado** (CLARIFICACION #37): depende de MEM-04/MEM-05, que no existen todavía; se implementa con esos endpoints (004-T8).

## 3. Requisitos funcionales (EARS)

| ID | Requisito | Endpoint |
|---|---|---|
| 004-R1 | Cuando un miembro activo envíe juego (1–60), condición (1–280), recompensa (3–80), fecha límite futura, fianza permitida por el grupo y destinatario, el sistema deberá crear el contrato en `open`, versión 1, y responder `201`. | CON-01 |
| 004-R2 | Si el grupo tiene menos de 3 miembros, entonces el sistema deberá responder `409 GROUP_TOO_SMALL`. | CON-01 |
| 004-R3 | Si `target_type` no es `group` ni `member`, el miembro retado no pertenece al grupo o es el propio retador, entonces el sistema deberá responder `422 INVALID_TARGET`. | CON-01 |
| 004-R4 | Cuando se cree o edite un contrato, el sistema deberá moderar juego, condición y recompensa; si alguno se rechaza deberá responder `422 CONTENT_REJECTED` y, si el moderador no responde, `503`. | CON-01, CON-04 |
| 004-R5 | Si la recompensa contiene cantidades de dinero, símbolos o palabras de moneda, entonces el sistema deberá responder `422`. | CON-01, CON-04 |
| 004-R6 | Cuando se publique un reto, el sistema deberá avisar al retado, o a todos los miembros menos el retador si es para el grupo (aviso `contract_received`). | CON-01 (NOT-05) |
| 004-R7 | Cuando un miembro pida el tablero, el sistema deberá listar los retos del grupo con filtros `status`, `mine`, `game` y `q`, paginados por cursor; y el detalle solo a miembros del grupo. | CON-02, CON-03 |
| 004-R8 | Mientras el reto esté en `open` y sin aceptar, el sistema deberá permitir al retador editarlo con `version` (bloqueo optimista) o cancelarlo; en cualquier otro estado deberá responder `409`. | CON-04, CON-05 |
| 004-R9 | Cuando el retado (o cualquier miembro salvo el retador, si es para el grupo) acepte con `Idempotency-Key`, el sistema deberá pasar el reto a `in_progress` y retener la fianza en una sola transacción; el primero que acepte un reto de grupo se queda con él. | CON-06 |
| 004-R10 | Si el saldo es menor que la fianza, entonces `402 INSUFFICIENT_POINTS` sin cambios; si quien acepta es el retador, `409 CANNOT_ACCEPT_OWN_CONTRACT`; si va dirigido a otro, `403 NOT_CONTRACT_TARGET`. | CON-06 |
| 004-R11 | Cuando el retado rechace un reto dirigido a él, el sistema deberá pasarlo a `cancelled` sin penalización. | CON-07 |
| 004-R12 | Cuando llegue la fecha límite sin evidencia, la tarea programada deberá pasar el reto a `expired` y, si estaba aceptado, entregar la fianza al retador. | Interno (ContractDeadlineWorker) |
| 004-R13 | Cuando se registre la primera evidencia aprobada, el sistema deberá pasar el reto de `in_progress` a `voting` y congelarlo. | EVI-02 |
| 004-R14 | Mientras un contrato esté en un estado final (`fulfilled`, `cheating`, `expired`, `cancelled`), el sistema deberá rechazar cualquier transición. | Todas |
| 004-R15 | Cuando ocurra cualquier transición, el sistema deberá escribirla en la auditoría encadenada por hash, visible en solo lectura para los administradores del grupo. | AUD-01 |

## 4. Requisitos no funcionales

- Aislamiento por grupo y verificación de pertenencia en cada consulta (anti-IDOR).
- Tablero en caché 30 s por versión del grupo; se invalida al crear, editar, aceptar o cancelar.
- Transacciones MongoDB para estado + saldo + libro de puntos; saldo nunca negativo.
- Los puntos no son dinero: sin símbolos ni conversiones (regla de oro).
- Sin condición, recompensa ni frases en los logs.

## 5. Endpoints y esquemas

CON-01 (`POST /contracts` y alias `POST /groups/{gid}/contracts`), CON-02, CON-03, CON-04, CON-05, CON-06, CON-07, AUD-01. Esquemas en `api/openapi.yaml`: `CreateContractRequest`, `UpdateContractRequest`, `ContractActionRequest`, `Contract`, `AuditEvent`, `Error`.

## 6. Criterios de aceptación

| ID | Requisito | Escenario | Evidencia |
|---|---|---|---|
| 004-AC1 | R1 | Creación válida → 201 en `open` | `CoreFlowTests.RegisterGroupContractEvidenceTournamentsAndLogout_WorksEndToEnd` |
| 004-AC2 | R1 | Ruta anidada y `group_id` contradictorio → 422; no miembro → 403 | `CoreFlowTests.NestedContractRoute_…`, `Contract_FromNonMember_ReturnsForbidden` |
| 004-AC3 | R2 | Grupo de 2 → 409 | `JuryFlowTests.Contract_InGroupWithFewerThanThreeMembers_Returns409` |
| 004-AC4 | R3 | Auto-reto → 422; retado ajeno al grupo → 422 | `CoreFlowTests.Contract_TargetingYourself_Returns422`, `Spec004ContractTests.TargetOutsideTheGroup_Returns422InvalidTarget` |
| 004-AC5 | R4 | Juego o condición rechazados → 422 al crear; juego rechazado al editar → 422 | `Spec004ContractTests.ModeratedText_…`, `EditWithModeratedGame_Returns422` |
| 004-AC6 | R5 | Recompensa con dinero → 422 | `UnitTest/ContractRulesTests` |
| 004-AC7 | R1 | Fianza fuera de las opciones del grupo → 422 `INVALID_BOND` | `Spec004ContractTests.BondNotAllowedByGroup_Returns422InvalidBond` |
| 004-AC8 | R6 | Reto dirigido: solo el retado recibe el aviso | `Spec004ContractTests.DirectedContract_NotifiesOnlyTheTarget` |
| 004-AC9 | R7 | Tablero con filtros y detalle para miembros | `SocialFlowTests.Contracts_BoardDetailAcceptAndReject` |
| 004-AC10 | R8 | Editar y cancelar en `open`; después → 409 | `AccountAndPushTests.Challenger_EditsAndCancelsOpenContract_AdminSeesAudit` |
| 004-AC11 | R9, R10 | Aceptar retiene fianza; propio → 409; otro → 403; saldo corto → 402; reintento con la misma clave no cobra dos veces | `SocialFlowTests`, `JuryFlowTests.Accept_RequiresIdempotencyKey_AndReplaysTheFirstResponse` |
| 004-AC12 | R11 | Rechazar → `cancelled` | `SocialFlowTests.Contracts_BoardDetailAcceptAndReject` |
| 004-AC13 | R12 | Vencido sin evidencia → `expired` y fianza al retador | `JuryFlowTests.ContractPastDeadline_WithoutEvidence_ExpiresAndForfeitsBond` |
| 004-AC14 | R13 | Primera evidencia → `voting` | `CoreFlowTests.Evidence_FirstEvidenceMovesContractToVoting` |
| 004-AC15 | R14 | Aceptar dos veces → 409 | `SocialFlowTests` |
| 004-AC16 | R15 | El administrador ve `contract.created`, `contract.updated`, `contract.cancelled` | `AccountAndPushTests.Challenger_EditsAndCancelsOpenContract_AdminSeesAudit` |

## 7. Preguntas abiertas

Ninguna que bloquee esta spec. Q-022 (cancelación por el administrador) quedó fuera de alcance y no cambia ningún requisito de arriba.

## 8. Verificación contra la constitución

| Regla | Cumple |
|---|---|
| §6.3 estados y transiciones, finales no se reabren | Sí (R8–R14) |
| §6.3 un retado; persona o grupo; fecha límite y fianza del publicador | Sí (R1, R3) |
| §6.3 fianzas configurables, `402` sin saldo, saldo nunca negativo | Sí (R1, R10) |
| §6.3 recompensa 3–80 moderada y sin dinero | Sí (R4, R5) |
| §6.3 vencido sin evidencia → `expired`, fianza al retador | Sí (R12) |
| §6.3 auditoría encadenada en cada transición | Sí (R15) |
| §7.1 `Idempotency-Key` al aceptar | Sí (R9) |
| §3.9 texto libre moderado | Sí, ahora también el juego (004-T7) |

## 9. Plan de pruebas

[Plan de pruebas](../../docs/plan-de-pruebas.md), sección Contratos; pruebas de abuso: IDOR (no miembro), auto-reto, doble aceptación, reintento idempotente, saldo insuficiente.

## 10. Riesgos y deuda

- 004-T8 (retos del creador que sale) no se puede cumplir hasta que existan MEM-04/MEM-05.
- La moderación del juego agrega una llamada a Comprehend por reto; dentro de la capa gratuita para el volumen de la evaluación.
- La detección de toxicidad de Comprehend solo admite inglés (`LanguageCode.En`); groserías en español pueden pasar. Mitigación posible: lista local de palabras en español antes de Comprehend (se decide en la spec 007 de moderación).

## Aprobación

| Rol | Persona | Fecha | Resultado |
|---|---|---|---|
| Product Owner | David M. | 2026-11-06 | Aprobada |
| Revisión técnica | Kevin H. | Pendiente | — |
| Comprobabilidad | José G. | Pendiente | — |

Expediente: [plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).
