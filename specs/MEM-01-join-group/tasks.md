# Tareas — MEM-01 — POST /api/v1/groups/join

[Spec](spec.md) · [plan](plan.md).

- [x] MEM-01-T1 — RF1: code de 6 alfanuméricos; se normaliza a mayúsculas.
- [x] MEM-01-T2 — RF2: código inexistente o inactivo → 404.
- [x] MEM-01-T3 — RF3: ya miembro → 409 ALREADY_MEMBER (también si el índice único gana la carrera).
- [x] MEM-01-T4 — RF4: máximo 5 grupos por usuario → 409 GROUP_LIMIT_REACHED.
- [x] MEM-01-T5 — RF5: máximo 20 miembros (`Auth:MaxMembersPerGroup`); incremento atómico condicionado de member_count → 409 GROUP_FULL.
- [x] MEM-01-T6 — RF6: alta como member con 100 puntos; si falla el alta se revierte member_count.
- [x] MEM-01-T7 — RF7: límite de intentos: 10 cada 10 min por usuario → 429.
- [x] MEM-01-T8 — RF8: auditar `group.member_joined`; invalidar caché del usuario y del grupo.
- [x] T9 — Pruebas: `SocialFlowTests`, `SocialFlowTests.Join_FullGroup_Returns409`, `SocialFlowTests.cs`, `AuthorizationTests.cs`.
- [x] T10 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
