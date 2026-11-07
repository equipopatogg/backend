# Plan — DEV-01..03 y NOT-04/05 — Dispositivos, preferencias y push con FCM

[Spec](spec.md).

## Enfoque

Endpoint de Minimal API con validación por `ValidationFilter`, reglas en el servicio del módulo y acceso a datos solo por repositorios (Mongo en producción, memoria en pruebas). Respuestas `{ data, meta }` y errores `{ code, message, request_id }`.
El código se adelantó (v0.5–0.9) y este plan se reconstruyó a partir de él el 2026-11-06 para completar el expediente SDD (constitución §9.3).

## Archivos

| Ruta | Papel |
|---|---|
| `Pactogg.Api/Modules/Devices/DeviceModule.cs` | Endpoint |
| `Pactogg.Api/Infrastructure/Push/PushDispatcher.cs` | Reglas y servicio |
| `Pactogg.Api/Infrastructure/Push/FcmPushSender.cs` | Reglas y servicio |
| `api/openapi.yaml` | Contrato |

## Pruebas

- `ProfileContractAuditPushTests.Push_RespectsPreferences_AndDevicesCanBeRemoved`
- `ProfileContractAuditPushTests.UnregisteredFcmToken_IsDeleted`
- `PushPreferencesTests.cs`

## Riesgos

Los de la sección de riesgos y deuda de la spec; sin dependencias nuevas.
