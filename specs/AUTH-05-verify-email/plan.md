# Plan — AUTH-05 — Verificación de correo

[Spec](spec.md).

## Enfoque

Endpoint de Minimal API con validación por `ValidationFilter`, reglas en el servicio del módulo y acceso a datos solo por repositorios (Mongo en producción, memoria en pruebas). Respuestas `{ data, meta }` y errores `{ code, message, request_id }`.
El código se adelantó (v0.5–0.9) y este plan se reconstruyó a partir de él el 2026-11-06 para completar el expediente SDD (constitución §9.3).

## Archivos

| Ruta | Papel |
|---|---|
| `Pactogg.Api/Modules/Auth/AuthEndpoints.cs` | Endpoint |
| `Pactogg.Api/Modules/Auth/AuthService.cs` | Reglas y servicio |
| `Pactogg.Api/Modules/Auth/AccountService.cs` | Reglas y servicio |
| `api/openapi.yaml` | Contrato |

## Pruebas

- `AccountAndPushTests.cs`
- `AccountFlowTests.Register_SendsVerification_AndGroupsRequireVerifiedEmail`

## Riesgos

Los de la sección de riesgos y deuda de la spec; sin dependencias nuevas.
