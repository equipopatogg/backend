# Tareas — MEM-02 — GET /api/v1/groups/{id}/members

[Spec](spec.md) · [plan](plan.md).

- [x] MEM-02-T1 — RF1: solo miembros (403) de un grupo existente (404).
- [x] MEM-02-T2 — RF2: q = prefijo del apodo sobre nick_lower (regex anclada y escapada, índice ix_group_members_group_nick).
- [x] MEM-02-T3 — RF3: orden de ingreso (_id ascendente) con cursor; limit 1–50.
- [x] MEM-02-T4 — RF4: cada elemento: user_id, nickname, avatar_id, role, points_balance, joined_at.
- [x] MEM-02-T5 — RF5: caché 60 s por versión del grupo (se invalida al unirse alguien, aceptar retos o cambiar un apodo).
- [x] T6 — Pruebas: `SocialFlowTests`, `SocialFlowTests.cs`, `AuthorizationTests.cs`.
- [x] T7 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
