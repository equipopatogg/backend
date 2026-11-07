# Tareas — USR-04 — DELETE /api/v1/users/me (eliminar cuenta)

[Spec](spec.md) · [plan](plan.md).

- [x] USR-04-T1 — USR04-R1: Si la contraseña no coincide o `confirm` no es, entonces `422` sin borrar nada (USR04-D2: reautenticación para una acción irreversible, ASVS 2).
- [x] USR-04-T2 — USR04-R2: Cuando se confirme, el sistema deberá cancelar los retos activos de la persona con devolución de fianzas y sacarla de todos sus grupos (mismo efecto que MEM-05)
- [x] USR-04-T3 — USR04-R3: Si la persona era creadora de un grupo, el grupo deberá pasar al administrador más antiguo (o al miembro más antiguo) y avisarle; si no queda nadie, el grupo se
- [x] USR-04-T4 — USR04-R4: El sistema deberá borrar las fotos de evidencia de S3, los avisos, los bloqueos, los dispositivos y los comentarios de voto, y cerrar sus compromisos abiertos (
- [x] USR-04-T5 — USR04-R5: El sistema deberá revocar todas las sesiones y el access token actual.
- [x] USR-04-T6 — USR04-R6: El sistema deberá borrar correo, contraseña, identificador de Google, avatar y consentimiento, y dejar el apodo "Usuario eliminado"; contratos, votos, movimient
- [x] T7 — Pruebas: `DeleteAccountTests.WrongPasswordOrMissingConfirmation_Is422`, `DeleteAccountTests.Delete_AnonymizesRevokesAndHandsOverOwnedGroup`, `DeleteAccountTests.SoleMember_GroupIsDeleted`.
- [x] T8 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
