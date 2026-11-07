# Tareas — GRP-03 — GET /api/v1/groups/{id}

[Spec](spec.md) · [plan](plan.md).

- [x] GRP-03-T1 — RF1: id inválido o grupo inexistente → 404.
- [x] GRP-03-T2 — RF2: no miembro → 403 NOT_GROUP_MEMBER (sin revelar datos).
- [x] GRP-03-T3 — RF3: devuelve el mismo esquema Group con my_role y my_points actuales.
- [x] GRP-03-T4 — RF4: sin caché propia (2 lecturas por índice); el Excel pide 120 s: se omitió para que my_points sea exacto tras aceptar retos.
- [x] T5 — Pruebas: `SocialFlowTests`, `SocialFlowTests.cs`, `AuthorizationTests.cs`.
- [x] T6 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
