# 003 — Grupos

**Fecha:** 2026-11-04 · **Estado:** Implementado y verificado · **Sprint:** 2
**Historias:** ID 30 Crear grupo privado · ID 31 Código de invitación · ID 32 Unirse mediante código · ID 159 Puntos sin valor monetario.
**Expedientes:** [GRP-01](../GRP-01-create-group/spec.md), [GRP-02](../GRP-02-list-groups/spec.md), [GRP-03](../GRP-03-get-group/spec.md), [MEM-01](../MEM-01-join-group/spec.md), [MEM-02](../MEM-02-list-members/spec.md), [PTS-01](../PTS-01-my-points/spec.md), [PTS-02](../PTS-02-points-ranking/spec.md).

## 1. Contexto y objetivo

Grupos privados de amigos con su propia economía de puntos: crearlos, invitar con un código, unirse y ver miembros y saldos.

## 2. Requisitos funcionales

| ID | Endpoint | Regla clave |
|---|---|---|
| ID 30 | `POST /groups` | Nombre 2–40; creador admin con 100 puntos; máximo 5 grupos por usuario; jurados 1–7, consenso 0.50–1 (CLARIFICACION #72, DESC_BACK 7.6), fianzas 1–5 valores 10–200 |
| ID 31 | `POST /groups` (código) | `invite_code` de 6 caracteres criptográfico sin ambiguos (sin 0/O/1/I), único |
| ID 32 | `POST /groups/join` | Código válido y activo (404); ya miembro (409); máximo 20 miembros (409 `GROUP_FULL`) con incremento atómico; 10 intentos/10 min |
| — | `GET /groups`, `GET /groups/{id}`, `GET /groups/{id}/members` | Solo miembros; búsqueda por nombre y por prefijo de apodo; cursor |
| ID 159 | `GET /groups/{gid}/points/me`, `/points/ranking` | Saldo por grupo; libro inmutable; no existe compra ni venta de puntos |

## 3. Requisitos no funcionales

Toda lectura filtra por pertenencia; caché por versión de grupo (60 s); saldo nunca negativo.

## 4. Contrato

`api/openapi.yaml` (tags Groups y Points).

## 5. Arquitectura y decisiones

`Modules/Groups` (`GroupService`, `GroupAccess`), `Modules/Points`. Decisiones F2-D25 (caché por versiones) y F2-D26 (20 miembros).

## 6. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| 003-AC1 | Crear grupo → admin con 100 puntos y código | `CoreFlowTests` |
| 003-AC2 | Unirse con código en minúsculas → member con 100 puntos | `SocialFlowTests.Groups_JoinListDetailAndMembers` |
| 003-AC3 | Grupo lleno → 409 `GROUP_FULL`; repetido → 409 `ALREADY_MEMBER` | `SocialFlowTests` |
| 003-AC4 | Ajeno no ve grupo, miembros ni puntos (403) | `SocialFlowTests`, `JuryFlowTests` |
| 003-AC5 | Puntos: movimientos del libro y filtro `type`; tipo inventado → 422 | `JuryFlowTests` |

## 7. Plan de pruebas

[Plan de pruebas](../../docs/plan-de-pruebas.md), sección Grupos.

## 8. Riesgos y deuda

Salir/expulsar miembros (MEM-03..05) fuera del sprint. Moderación del nombre de grupo pendiente (Q-021).

Expediente: [plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).
