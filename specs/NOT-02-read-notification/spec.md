# NOT-02 — PATCH /api/v1/notifications/{id}/read

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas en verde).
**Contrato:** `api/openapi.yaml` (v0.6.0)

## 1. Objetivo

Marcar un aviso como leído.

## 2. Requisitos funcionales

- RF1: Solo el dueño; si no existe o es de otro → 404 (no revela que existe).
- RF2: Idempotente.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 404 | `NOT_FOUND` | No existe o es ajeno |

## 4. Implementación

`Pactogg.Api/Modules/Notifications/NotificationService.cs (MarkReadAsync)`

## 5. Criterios de aceptación

| Escenario | Evidencia |
|---|---|
| Dueño → read=true; otro usuario → 404 | IntegrationTest/JuryFlowTests.cs |

401 sin token: `SecurityTest/AuthorizationTests.cs`.
