# Plan — CON-01

[Spec](spec.md).

## Enfoque

Validar grupo/pertenencia, fecha, fianza, objetivo, recompensa y moderación; persistir contrato abierto y auditar.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Modules/Contracts/ContractEndpoints.cs` | Rutas |
| `Pactogg.Api/Modules/Contracts/ContractService.cs` | Servicio |
| `Pactogg.Api/Modules/Contracts/ContractRules.cs` | Recompensa |
| `IntegrationTest/CoreFlowTests.cs` | Flujo/403/anidada |
| `api/openapi.yaml` | Contrato |

## Dependencias

MongoDB, moderación, auditoría.

## Pruebas

Creación, 403, ruta anidada, recompensa, fianza/objetivo.

## Riesgos

Auto-reto y nomenclatura de objetivo; pruebas de fianza/objetivo pendientes.

## Orden

1. DTO y validación.
2. Servicio y persistencia.
3. Endpoint y autorización.
4. Pruebas, OpenAPI y checklist.

## Cierre

Checklist, `Memoria.md` y `specs/validation.md`.

