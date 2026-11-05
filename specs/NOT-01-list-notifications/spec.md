# NOT-01 — GET /api/v1/notifications

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas en verde).
**Contrato:** `api/openapi.yaml` (v0.6.0)

## 1. Objetivo

ID 69 · Aviso de reto recibido (dentro de la app).

## 2. Requisitos funcionales

- RF1: Solo los avisos propios; unread=true filtra no leídos; cursor; meta.unread_count.
- RF2: Se generan internamente (NOT-05): reto recibido (al retado o a todo el grupo), reto aceptado, te toca votar, votación empatada, reto resuelto, compromiso pedido/saldado.
- RF3: Mejor esfuerzo: si guardar el aviso falla, la acción principal no falla. TTL de 90 días (ttl_notifications_90d).

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |

## 4. Implementación

`Pactogg.Api/Modules/Notifications/NotificationService.cs`

## 5. Criterios de aceptación

| Escenario | Evidencia |
|---|---|
| El retado recibe contract_received; el jurado vote_requested | IntegrationTest/JuryFlowTests.cs |

401 sin token: `SecurityTest/AuthorizationTests.cs`.
