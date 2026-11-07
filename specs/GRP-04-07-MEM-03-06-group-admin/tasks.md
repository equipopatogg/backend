# Tareas — GRP-04..07 y MEM-03..06 — Administración del grupo y miembros

[Spec](spec.md) · [plan](plan.md).

- [x] GRP-04-T1 — ADM-R1: Si quien llama no es `admin` del grupo, entonces GRP-04, GRP-06, GRP-07, MEM-03 y MEM-04 deberán responder `403 NOT_GROUP_ADMIN`; GRP-05 solo lo puede hacer el 
- [x] GRP-04-T2 — ADM-R2: Cuando un admin cambie el nombre o los ajustes, el sistema deberá aplicar las mismas reglas que GRP-01 (nombre 2–40 moderado, jurados 1–7, consenso 0.50–1, 1–5 
- [x] GRP-04-T3 — ADM-R3: Cuando un admin regenere el código, el sistema deberá generar uno nuevo único y el anterior deberá dejar de funcionar (CLARIFICACION #28).
- [x] GRP-04-T4 — ADM-R4: Mientras el código esté desactivado, unirse con él deberá responder `404` (CLARIFICACION #27).
- [x] GRP-04-T5 — ADM-R5: Si el objetivo de MEM-03 o MEM-04 es el creador, entonces `409 CANNOT_MODIFY_OWNER` (CLARIFICACION #24, #25); si es uno mismo en MEM-04, `422` (para salir se us
- [x] GRP-04-T6 — ADM-R6: Cuando un miembro salga (MEM-05) o sea expulsado (MEM-04), el sistema deberá cancelar sus retos activos como retador o retado (`open`, `in_progress`, `voting`),
- [x] GRP-04-T7 — ADM-R7: Si el creador intenta salir, entonces `409 OWNER_CANNOT_LEAVE`; para irse debe eliminar el grupo (ADM-D1).
- [x] GRP-04-T8 — ADM-R8: Cuando alguien que salió o fue expulsado vuelva a unirse con un código válido, el sistema deberá reactivarlo con el saldo que tenía.
- [x] GRP-04-T9 — ADM-R9: Cuando el creador elimine el grupo, el sistema deberá cancelar todos los retos activos con devolución de fianzas, dar de baja a todos los miembros y hacer borra
- [x] GRP-04-T10 — ADM-R10: Cuando cambie un rol o expulsen a alguien, el sistema deberá avisar a esa persona (aviso `group_role_changed` o `group_member_removed`, CLARIFICACION #116) y au
- [x] GRP-04-T11 — ADM-R11: Cuando un miembro pida MEM-06, el sistema deberá devolver su rol, saldo y fecha de ingreso en ese grupo.
- [x] T12 — Pruebas: `GroupAdminTests.Admin_EditsNameAndSettings_MemberGets403`, `GroupAdminTests.RegenerateAndDisableInviteCode`, `GroupAdminTests.Roles_CreatorCannotBeTouched_PromotedMemberIsNotified`, `GroupAdminTests.KickedMember_ContractsCancelledBondRefunded_CanRejoinWithBalance`, `GroupAdminTests.LeaveAndMyMembership`, `GroupAdminTests.OwnerDeletesGroup_CancelsContractsAndFreesMembers`.
- [x] T13 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
