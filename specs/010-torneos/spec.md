# 010 — Torneos entre grupos

**Fecha:** 2026-11-07 · **Estado:** Aprobada; por implementar · **Sprint:** 4 (fila 102)
**Historias:** torneos exprés entre grupos, inscripción en puntos, bracket con byes, reporte de resultados (ID 115), bolsa al campeón.
**Endpoints del CRUD:** TOR-01..09 y MAT-01..07 (TOR-02 hecho; los demás por hacer).
**Fuentes:** constitución §6.7 y §7.1; CLARIFICACION #1, #3, #96–#107, #116; Descripción del Backend 7.8; CRUD hojas Torneos y Partidas; D-20.

## 1. Contexto y objetivo

Un grupo organiza un torneo de un juego y otros grupos se inscriben como equipos. Cada partida es 1 contra 1 entre un representante de cada grupo; el grupo ganador avanza por eliminación directa y el campeón se lleva la bolsa de puntos.

## 2. Fuera de alcance

Torneos individuales, otros formatos (solo eliminación directa), torneos oficiales del administrador de la plataforma, empates (se repite la partida) y chat del torneo.

## 3. Requisitos funcionales (EARS)

### Torneo

| ID | Requisito | Endpoint |
|---|---|---|
| 010-R1 | Cuando el administrador de un grupo cree un torneo con juego (1–60), tamaño 4, 8 o 16 grupos, inscripción de 0 a 500 puntos y fecha futura, el sistema deberá crearlo `open`, con un código de torneo de 6 caracteres, e inscribir a su grupo (010-D1). | TOR-01 |
| 010-R2 | Cuando un miembro de un grupo inscrito pida el detalle, el sistema deberá devolver grupos inscritos (nombre y número de miembros, sin datos personales), bolsa, estado y código (este solo al organizador y administradores inscritos). | TOR-03 |
| 010-R3 | Mientras el torneo esté `open`, el organizador podrá cambiar fecha, juego o tamaño (nunca por debajo de los inscritos); después, `409`. | TOR-04 |
| 010-R4 | Cuando el organizador cancele (`open` o `in_progress`), el sistema deberá devolver todas las inscripciones con movimientos compensatorios y avisar a los grupos. | TOR-05 |
| 010-R5 | Cuando el administrador de otro grupo inscriba a su grupo con el código y `Idempotency-Key`, el sistema deberá cobrar la inscripción a ese administrador en su grupo (`tournament_entry`), sumarla a la bolsa y agregar el grupo; sin saldo `402`, lleno o no `open` `409` (010-D2). | TOR-06 |
| 010-R6 | Cuando el administrador saque a su grupo mientras esté `open`, el sistema deberá devolver la inscripción; el grupo organizador no puede salir (debe cancelar). | TOR-07 |
| 010-R7 | Cuando el organizador inicie con al menos 2 grupos, el sistema deberá cerrar inscripciones, sortear el orden, completar con byes hasta la siguiente potencia de 2 (byes avanzan solos) y crear todas las partidas enlazadas con `next_match_id`. | TOR-08 |
| 010-R8 | Cuando el organizador invite grupos, el sistema deberá avisar a los administradores de esos grupos solo si él también es miembro de ellos; para los demás comparte el código fuera de la app (010-D3). | TOR-09 |

### Partidas

| ID | Requisito | Endpoint |
|---|---|---|
| 010-R9 | Cuando un miembro de un grupo inscrito pida el bracket o una partida, el sistema deberá devolver rondas, posiciones, grupos, marcador y estado. | MAT-01, MAT-02 |
| 010-R10 | Cuando un miembro de uno de los dos grupos reporte el resultado con marcador y foto moderada, el sistema deberá guardarlo `reported` y registrar a esa persona como representante de su grupo; un empate se rechaza con `422` (se repite la partida). | MAT-03 |
| 010-R11 | Cuando un miembro del grupo rival confirme, el sistema deberá marcar `confirmed`, avanzar al grupo ganador a la siguiente partida y registrar al representante rival. | MAT-04 |
| 010-R12 | Cuando un miembro del grupo rival dispute con motivo (máx. 140, moderado), el sistema deberá marcar `disputed` y avisar al organizador. | MAT-05 |
| 010-R13 | Cuando el organizador resuelva una disputa, o confirme un reporte que lleva más de 24 h sin respuesta, o declare abandono, el sistema deberá fijar el ganador y avanzar; si el organizador juega esa partida, `409` (CLARIFICACION #105). | MAT-06 |
| 010-R14 | Cuando se confirme la final, el sistema deberá marcar el torneo `finished`, repartir la bolsa en partes iguales entre los representantes del grupo campeón que jugaron (el residuo al primero en orden de juego) con `tournament_prize`, otorgar el trofeo y avisar a todos los grupos. | Interno |
| 010-R15 | Cuando un miembro pida su siguiente partida, el sistema deberá devolver la partida pendiente más próxima de cualquiera de sus grupos o `null`. | MAT-07 |
| 010-R16 | Si un torneo `in_progress` se queda con menos de 2 grupos activos, entonces el sistema deberá cancelarlo y devolver las inscripciones (CLARIFICACION #101). | Interno |

## 4. Requisitos no funcionales

Transacciones para inscripción, salida, cancelación y premios; `Idempotency-Key` en TOR-06 y en los movimientos de puntos; bracket en caché 10 s; partidas indexadas por `tournament_id` y grupos; un torneo por grupo organizador activo a la vez (límites de M0); avisos `tournament_*` respetando preferencias.

## 5. Endpoints y esquemas

TOR-01..09, MAT-01..07 (rutas en la lista de endpoints). Esquemas: `CreateTournamentRequest`, `Tournament`, `JoinTournamentRequest { group_id, code }`, `Match`, `ReportMatchRequest { winner_group_id, p1_score, p2_score }` (multipart con `file`), `DisputeRequest { reason }`, `ResolveMatchRequest { winner_group_id, reason }`.

## 6. Criterios de aceptación

| ID | Requisito | Dado / cuando / entonces |
|---|---|---|
| 010-AC1 | R1, R5 | Crear con 50 puntos → organizador pagó 50; otro admin se inscribe → bolsa 100; sin saldo → 402; reintento con la misma clave no cobra dos veces |
| 010-AC2 | R3, R6 | Editar o salir en `open` → OK; tras iniciar → 409; organizador no puede salir |
| 010-AC3 | R7 | 3 grupos → bracket de 4 con un bye que avanza solo; 5 grupos → 8 con 3 byes |
| 010-AC4 | R10, R11 | Reporte con empate → 422; reporte válido + confirmación del rival → el ganador aparece en la siguiente partida |
| 010-AC5 | R12, R13 | Disputa → organizador resuelve; organizador que juega esa partida → 409; reporte sin respuesta 24 h → el organizador confirma |
| 010-AC6 | R14 | Final confirmada → `finished`, bolsa repartida, suma de premios = bolsa, trofeo a los que jugaron |
| 010-AC7 | R4, R16 | Cancelar → todos recuperan su inscripción (movimientos compensatorios) |
| 010-AC8 | R2, R9, R15 | Ajeno al torneo → 403; siguiente partida correcta para un miembro |

## 7. Decisiones (aprobadas por el PO el 2026-11-07)

- **010-D1:** el grupo del organizador queda inscrito al crear y paga como cualquier otro (CLARIFICACION #97: "lo crea y lo inscribe").
- **010-D2:** la inscripción la paga el administrador que inscribe, de su saldo en su grupo (los grupos no tienen saldo propio, §6.6); el premio va a los representantes que jugaron por el campeón (CLARIFICACION #106).
- **010-D3:** los grupos no se ven entre sí (privacidad, §3.5): otro grupo entra con el código del torneo; la invitación por la app solo llega a grupos donde el organizador también es miembro.
- **010-D4:** el representante de un grupo en una partida es el miembro que reporta, confirma o disputa por ese grupo; no se designa antes.

## 8. Preguntas abiertas

Ninguna.

## 9. Verificación contra la constitución

§6.7 entre grupos, 4/8/16, byes, 1 contra 1, reporte con foto moderada, confirma rival u organizador, bolsa al campeón ✔ · §6.6 movimientos `tournament_entry` y `tournament_prize` ✔ · §7.1 `Idempotency-Key` al inscribirse ✔ · §3.5 aislamiento por grupo ✔ (010-D3).

## Aprobación

| Rol | Persona | Fecha | Resultado |
|---|---|---|---|
| Product Owner | David M. | 2026-11-07 | Aprobada |
| Revisión técnica | Kevin H. | — | Pendiente |
| Comprobabilidad | José G. | — | Pendiente |

Expediente: [plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).
