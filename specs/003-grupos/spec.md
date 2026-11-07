# 003 — Grupos

**Fecha:** 2026-11-06 · **Estado:** Aprobada e implementada · **Sprint:** 2
**Historias:** ID 30 Crear grupo privado · ID 31 Código de invitación · ID 32 Unirse mediante código · ID 159 Puntos sin valor monetario.
**Expedientes:** [GRP-01](../GRP-01-create-group/spec.md), [GRP-02](../GRP-02-list-groups/spec.md), [GRP-03](../GRP-03-get-group/spec.md), [MEM-01](../MEM-01-join-group/spec.md), [MEM-02](../MEM-02-list-members/spec.md), [PTS-01](../PTS-01-my-points/spec.md), [PTS-02](../PTS-02-points-ranking/spec.md).
**Fuentes:** constitución §6.2 y §6.6; CLARIFICACION #21–#29, #72 y #138; CRUD hojas Grupos, Miembros y Puntos.

## 1. Contexto y objetivo

Grupos privados de amigos con su propia economía de puntos: crearlos, invitar con un código, unirse y ver miembros y saldos.

## 2. Fuera de alcance

Estas funciones están en el CRUD pero no en las historias 30, 31, 32 y 159. Se especificaron e implementaron aparte el 2026-11-06 (v0.9.0) en [GRP-04..07 y MEM-03..06](../GRP-04-07-MEM-03-06-group-admin/spec.md) y [PTS-03](../PTS-03-COM-02-05-ACH-03-details/spec.md):

- GRP-04 editar grupo, GRP-05 eliminar grupo, GRP-06 regenerar código, GRP-07 activar o desactivar código.
- MEM-03 cambiar rol, MEM-04 expulsar, MEM-05 salir del grupo, MEM-06 mi rol y saldo.
- PTS-03 detalle de un movimiento.

## 3. Requisitos funcionales (EARS)

| ID | Requisito | Endpoint |
|---|---|---|
| 003-R1 | Cuando un usuario verificado cree un grupo con nombre de 2–40 caracteres, el sistema deberá crearlo privado, con el creador como `admin` y 100 puntos, y responder `201`. | GRP-01 |
| 003-R2 | Si el nombre del grupo es rechazado por moderación, entonces el sistema deberá responder `422 CONTENT_REJECTED` sin guardar nada; si el moderador no responde, `503` (F2-D52). | GRP-01 |
| 003-R3 | Si el usuario ya pertenece a 5 grupos, entonces el sistema deberá responder `409 GROUP_LIMIT_REACHED`. | GRP-01, MEM-01 |
| 003-R4 | Cuando se cree el grupo, el sistema deberá generar un `invite_code` único de 6 caracteres con generador criptográfico y sin caracteres ambiguos (0, O, 1, I). | GRP-01 |
| 003-R5 | Si los ajustes no cumplen jurados 1–7, consenso 0.50–1 o fianzas de 1 a 5 valores entre 10 y 200, entonces el sistema deberá responder `422`. | GRP-01 |
| 003-R6 | Cuando un usuario verificado envíe un código válido y activo (sin distinguir mayúsculas), el sistema deberá unirlo como `member` con 100 puntos. | MEM-01 |
| 003-R7 | Si el código no existe o está desactivado, entonces `404`; si ya es miembro, `409 ALREADY_MEMBER`; si el grupo tiene 20 miembros, `409 GROUP_FULL` con incremento atómico. | MEM-01 |
| 003-R8 | Mientras un usuario intente unirse más de 10 veces en 10 minutos, el sistema deberá responder `429`. | MEM-01 |
| 003-R9 | Cuando un miembro pida sus grupos, el detalle o los miembros, el sistema deberá responder solo con grupos a los que pertenece, con búsqueda por nombre o prefijo de apodo y paginación por cursor. | GRP-02, GRP-03, MEM-02 |
| 003-R10 | Si quien consulta no es miembro, entonces el sistema deberá responder `403`. | GRP-03, MEM-02, PTS-01, PTS-02 |
| 003-R11 | Cuando un miembro consulte puntos, el sistema deberá devolver su saldo y movimientos del libro (filtrables por `type`) y el ranking del grupo; nunca deberá existir compra, venta ni canje de puntos. | PTS-01, PTS-02 |

## 4. Requisitos no funcionales

- Aislamiento por grupo en toda consulta; caché por versión del grupo (60 s) en Redis.
- Saldo nunca negativo; libro de puntos solo de inserción.
- Límites de M0: índices en `group_members(user_id, group_id)`, `groups(invite_code)`.

## 5. Endpoints y esquemas

GRP-01, GRP-02, GRP-03, MEM-01, MEM-02, PTS-01, PTS-02. Esquemas en `api/openapi.yaml`: `CreateGroupRequest`, `GroupSettings`, `Group`, `JoinGroupRequest`, `GroupMember`, `PointsMovement`, `MyPoints`, `RankingEntry`.

## 6. Criterios de aceptación

| ID | Requisito | Escenario | Evidencia |
|---|---|---|---|
| 003-AC1 | R1, R4 | Crear grupo → admin con 100 puntos y código | `CoreFlowTests` |
| 003-AC2 | R2 | Nombre rechazado → 422 y no se guarda | `SddDecisionTests.Q021_…` |
| 003-AC3 | R1 | Nombre de 1 y 41 caracteres → 422; 2 y 40 → 201 | `SddDecisionTests.Q019_GroupNameLengthIs2To40` |
| 003-AC4 | R3 | Sexto grupo → 409 | `SddDecisionTests.CreateGroup_SixthGroup_Returns409` |
| 003-AC5 | R5 | 9 jurados → 422 | `SddDecisionTests.CreateGroup_InvalidJurors_Returns422` |
| 003-AC6 | R6 | Unirse con código en minúsculas → member con 100 puntos | `SocialFlowTests.Groups_JoinListDetailAndMembers` |
| 003-AC7 | R7 | Lleno → 409 `GROUP_FULL`; repetido → 409 `ALREADY_MEMBER`; código inexistente → 404 | `SocialFlowTests` |
| 003-AC8 | R9, R10 | Ajeno no ve grupo, miembros ni puntos | `SocialFlowTests`, `JuryFlowTests` |
| 003-AC9 | R11 | Movimientos del libro y filtro `type`; tipo inventado → 422 | `JuryFlowTests` |
| 003-AC10 | R8 | Límite de intentos de unión | Política `join` en `RedisRateLimitMiddleware`; sin prueba dedicada (riesgo bajo) |

## 7. Preguntas abiertas

Ninguna. Q-016, Q-019 y Q-021 están resueltas.

## 8. Verificación contra la constitución

| Regla | Cumple |
|---|---|
| §6.2 código de 6, máximo 20 miembros, 5 grupos por usuario, roles admin/member, 100 puntos iniciales | Sí |
| §6.2 mínimo 3 para lanzar retos | Sí, se aplica en contratos (spec 004) |
| §6.2 regenerar/desactivar código, creador no expulsable | Fuera de alcance (GRP-06/07, MEM-03/04) |
| §3.5 aislamiento por grupo | Sí |
| §1 regla de oro sin dinero | Sí |

## 9. Plan de pruebas

[Plan de pruebas](../../docs/plan-de-pruebas.md), sección Grupos.

## 10. Riesgos y deuda

La administración del grupo quedó en su propio expediente (v0.9.0).

## Aprobación

| Rol | Persona | Fecha | Resultado |
|---|---|---|---|
| Product Owner | David M. | 2026-11-06 | Aprobada |
| Revisión técnica | Kevin H. | Pendiente | — |
| Comprobabilidad | José G. | Pendiente | — |

Expediente: [plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).
