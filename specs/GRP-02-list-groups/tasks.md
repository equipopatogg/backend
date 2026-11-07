# Tareas — GRP-02 — GET /api/v1/groups

[Spec](spec.md) · [plan](plan.md).

- [x] GRP-02-T1 — RF1: solo grupos donde el usuario es miembro activo.
- [x] GRP-02-T2 — RF2: q filtra por texto contenido en el nombre (máx. 40, regex escapada).
- [x] GRP-02-T3 — RF3: cursor por _id descendente; limit 1–50 (20 por defecto); meta {next_cursor, has_more}.
- [x] GRP-02-T4 — RF4: cada grupo incluye my_role y my_points.
- [x] GRP-02-T5 — RF5: caché 60 s por usuario y versión; se invalida al crear, unirse o aceptar un reto.
- [x] T6 — Pruebas: `SocialFlowTests.Groups_JoinListDetailAndMembers`, `SocialFlowTests`, `SocialFlowTests.cs`, `AuthorizationTests.cs`.
- [x] T7 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
