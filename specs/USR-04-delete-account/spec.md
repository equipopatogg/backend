# USR-04 — DELETE /api/v1/users/me (eliminar cuenta)

**Fecha:** 2026-11-06 · **Estado:** Aprobada e implementada (v0.9.0) · **Historia:** ID 29 Eliminación de cuenta y datos (requisito del docente) · **Feature:** [002](../002-cuentas-y-sesion/spec.md)
**Fuentes:** CLARIFICACION #12, #15, #16, #17, #18; constitución §6.1.

## Petición

`DELETE /api/v1/users/me` con cuerpo `{ "password": "…", "confirm": "ELIMINAR" }` → `204`. La contraseña es obligatoria si la cuenta tiene una (las de Google solo confirman).

## Requisitos (EARS)

- **USR04-R1:** Si la contraseña no coincide o `confirm` no es `ELIMINAR`, entonces `422` sin borrar nada (USR04-D2: reautenticación para una acción irreversible, ASVS 2).
- **USR04-R2:** Cuando se confirme, el sistema deberá cancelar los retos activos de la persona con devolución de fianzas y sacarla de todos sus grupos (mismo efecto que MEM-05).
- **USR04-R3:** Si la persona era creadora de un grupo, el grupo deberá pasar al administrador más antiguo (o al miembro más antiguo) y avisarle; si no queda nadie, el grupo se elimina (USR04-D1).
- **USR04-R4:** El sistema deberá borrar las fotos de evidencia de S3, los avisos, los bloqueos, los dispositivos y los comentarios de voto, y cerrar sus compromisos abiertos (`cancelled`).
- **USR04-R5:** El sistema deberá revocar todas las sesiones y el access token actual.
- **USR04-R6:** El sistema deberá borrar correo, contraseña, identificador de Google, avatar y consentimiento, y dejar el apodo "Usuario eliminado"; contratos, votos, movimientos y auditoría quedan con el identificador anónimo.

## Decisiones (aprobadas por el PO el 2026-11-06)

- **USR04-D1:** herencia del grupo al admin o miembro más antiguo.
- **USR04-D2:** confirmación con contraseña y la palabra ELIMINAR.

## Criterios de aceptación

| ID | Requisito | Prueba |
|---|---|---|
| USR04-AC1 | R1 | `DeleteAccountTests.WrongPasswordOrMissingConfirmation_Is422` |
| USR04-AC2 | R2–R6 | `DeleteAccountTests.Delete_AnonymizesRevokesAndHandsOverOwnedGroup` |
| USR04-AC3 | R3 | `DeleteAccountTests.SoleMember_GroupIsDeleted` |

Expediente: [plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).
