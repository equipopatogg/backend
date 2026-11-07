# Tareas — CON-03 — GET /api/v1/contracts/{id}

[Spec](spec.md) · [plan](plan.md).

- [x] CON-03-T1 — RF1: id inválido o inexistente → 404.
- [x] CON-03-T2 — RF2: solo miembros del grupo del reto (403).
- [x] CON-03-T3 — RF3: incluye accepted_by, accepted_at, closed_reason, evidence_count, version y updated_at.
- [x] CON-03-T4 — RF4: sin caché (lectura por _id); se prioriza ver el estado exacto antes de aceptar.
- [x] T5 — Pruebas: `SocialFlowTests`, `SocialFlowTests.cs`, `AuthorizationTests.cs`.
- [x] T6 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
