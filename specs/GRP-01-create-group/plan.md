# Plan — GRP-01

[Spec](spec.md).

## Enfoque

Validar settings, contar grupos activos, generar código único, crear grupo y miembro admin con rollback.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Modules/Groups/GroupEndpoints.cs` | Ruta |
| `Pactogg.Api/Modules/Groups/GroupService.cs` | Reglas |
| `Pactogg.Api/Modules/Groups/Dtos/GroupDtos.cs` | DTOs |
| `IntegrationTest/CoreFlowTests.cs` | Flujo |
| `api/openapi.yaml` | Contrato |

## Dependencias

MongoDB, auditoría, opciones Auth.

## Pruebas

Creación válida, settings inválidos, límite, 401 y rollback.

## Riesgos

Moderación de nombre ausente; límite y rollback sin prueba dedicada.

## Orden

1. DTO y validación.
2. Servicio y persistencia.
3. Endpoint y autorización.
4. Pruebas, OpenAPI y checklist.

## Cierre

Checklist, `Memoria.md` y `specs/validation.md`.

