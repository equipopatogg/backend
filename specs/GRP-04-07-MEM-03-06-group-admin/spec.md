# GRP-04..07 y MEM-03..06 — Administración del grupo y miembros

**Fecha:** 2026-11-06 · **Estado:** Aprobada e implementada (v0.9.0) · **Feature:** [003 Grupos](../003-grupos/spec.md)
**Fuentes:** CLARIFICACION #23–#28, #37; constitución §6.2; CRUD hojas Grupos y Miembros; spec 004 (004-T8).

## 1. Endpoints

| ID | Método y ruta | Quién | Respuesta |
|---|---|---|---|
| GRP-04 | `PATCH /api/v1/groups/{id}` `{ name?, settings? }` | admin | `200` grupo |
| GRP-05 | `DELETE /api/v1/groups/{id}` | creador | `204` |
| GRP-06 | `POST /api/v1/groups/{id}/invite-code/regenerate` | admin | `200` grupo con código nuevo |
| GRP-07 | `PATCH /api/v1/groups/{id}/invite-code` `{ active }` | admin | `200` grupo |
| MEM-03 | `PATCH /api/v1/groups/{id}/members/{uid}` `{ role: admin|member }` | admin | `200` miembro |
| MEM-04 | `DELETE /api/v1/groups/{id}/members/{uid}` | admin | `204` |
| MEM-05 | `DELETE /api/v1/groups/{id}/members/me` | miembro | `204` |
| MEM-06 | `GET /api/v1/groups/{id}/members/me` | miembro | `200` rol, saldo, fecha de ingreso |

## 2. Requisitos (EARS)

- **ADM-R1:** Si quien llama no es `admin` del grupo, entonces GRP-04, GRP-06, GRP-07, MEM-03 y MEM-04 deberán responder `403 NOT_GROUP_ADMIN`; GRP-05 solo lo puede hacer el creador (`403 NOT_GROUP_OWNER`).
- **ADM-R2:** Cuando un admin cambie el nombre o los ajustes, el sistema deberá aplicar las mismas reglas que GRP-01 (nombre 2–40 moderado, jurados 1–7, consenso 0.50–1, 1–5 fianzas de 10–200).
- **ADM-R3:** Cuando un admin regenere el código, el sistema deberá generar uno nuevo único y el anterior deberá dejar de funcionar (CLARIFICACION #28).
- **ADM-R4:** Mientras el código esté desactivado, unirse con él deberá responder `404` (CLARIFICACION #27).
- **ADM-R5:** Si el objetivo de MEM-03 o MEM-04 es el creador, entonces `409 CANNOT_MODIFY_OWNER` (CLARIFICACION #24, #25); si es uno mismo en MEM-04, `422` (para salir se usa MEM-05).
- **ADM-R6:** Cuando un miembro salga (MEM-05) o sea expulsado (MEM-04), el sistema deberá cancelar sus retos activos como retador o retado (`open`, `in_progress`, `voting`), devolver las fianzas retenidas y conservar su saldo para si vuelve (CLARIFICACION #26, #37).
- **ADM-R7:** Si el creador intenta salir, entonces `409 OWNER_CANNOT_LEAVE`; para irse debe eliminar el grupo (ADM-D1).
- **ADM-R8:** Cuando alguien que salió o fue expulsado vuelva a unirse con un código válido, el sistema deberá reactivarlo con el saldo que tenía.
- **ADM-R9:** Cuando el creador elimine el grupo, el sistema deberá cancelar todos los retos activos con devolución de fianzas, dar de baja a todos los miembros y hacer borrado lógico del grupo.
- **ADM-R10:** Cuando cambie un rol o expulsen a alguien, el sistema deberá avisar a esa persona (aviso `group_role_changed` o `group_member_removed`, CLARIFICACION #116) y auditar el cambio.
- **ADM-R11:** Cuando un miembro pida MEM-06, el sistema deberá devolver su rol, saldo y fecha de ingreso en ese grupo.

## 3. Decisiones

- **ADM-D1:** el creador no puede salir ni ser expulsado; si quiere irse, elimina el grupo (GRP-05). Aprobada por el PO el 2026-11-06 junto con esta spec.
- **ADM-D2:** quien sale o es expulsado puede volver con un código válido; si el admin no lo quiere de vuelta, regenera o desactiva el código.

## 4. Errores

`401`, `403 NOT_GROUP_MEMBER / NOT_GROUP_ADMIN / NOT_GROUP_OWNER`, `404`, `409 CANNOT_MODIFY_OWNER / OWNER_CANNOT_LEAVE`, `422 VALIDATION_ERROR / CONTENT_REJECTED`, `503` (moderación caída).

## 5. Criterios de aceptación

| ID | Requisito | Prueba |
|---|---|---|
| ADM-AC1 | R1, R2 | `GroupAdminTests.Admin_EditsNameAndSettings_MemberGets403` |
| ADM-AC2 | R3, R4 | `GroupAdminTests.RegenerateAndDisableInviteCode` |
| ADM-AC3 | R5, R10 | `GroupAdminTests.Roles_CreatorCannotBeTouched_PromotedMemberIsNotified` |
| ADM-AC4 | R6, R8 | `GroupAdminTests.KickedMember_ContractsCancelledBondRefunded_CanRejoinWithBalance` |
| ADM-AC5 | R7, R11 | `GroupAdminTests.LeaveAndMyMembership` |
| ADM-AC6 | R9 | `GroupAdminTests.OwnerDeletesGroup_CancelsContractsAndFreesMembers` |

Expediente: [plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).
