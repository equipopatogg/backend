# Tareas — NOT-01 — GET /api/v1/notifications

[Spec](spec.md) · [plan](plan.md).

- [x] NOT-01-T1 — RF1: Solo los avisos propios; unread=true filtra no leídos; cursor; meta.unread_count.
- [x] NOT-01-T2 — RF2: Se generan internamente (NOT-05): reto recibido (al retado o a todo el grupo), reto aceptado, te toca votar, votación empatada, reto resuelto, compromiso pedido
- [x] NOT-01-T3 — RF3: Mejor esfuerzo: si guardar el aviso falla, la acción principal no falla. TTL de 90 días (ttl_notifications_90d).
- [x] T4 — Pruebas: `JuryFlowTests.cs`, `AuthorizationTests.cs`.
- [x] T5 — `api/openapi.yaml` y `docs/API-movil.md` actualizados.

## Definición de terminado

Pruebas en verde, OpenAPI actualizado y checklist completo.
