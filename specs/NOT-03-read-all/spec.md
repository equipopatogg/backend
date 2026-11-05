# NOT-03 — POST /api/v1/notifications/read-all

**Fecha:** 2026-11-04
**Estado:** Implementado y verificado localmente (pruebas en verde).
**Contrato:** `api/openapi.yaml` (v0.6.0)

## 1. Objetivo

Marcar todos como leídos.

## 2. Requisitos funcionales

- RF1: Devuelve {updated: n}.

## 3. Errores

| HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |

## 4. Implementación

`Pactogg.Api/Modules/Notifications/NotificationService.cs (MarkAllReadAsync)`

## 5. Criterios de aceptación

| Escenario | Evidencia |
|---|---|
| Después, unread_count = 0 | IntegrationTest/JuryFlowTests.cs |

401 sin token: `SecurityTest/AuthorizationTests.cs`.
