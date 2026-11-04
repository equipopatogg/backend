# Plan — TOR-02

[Spec](spec.md).

## Enfoque

Validar filtros, verificar grupo, obtener grupos activos, consultar torneos/partidas, cachear y mapear respuesta.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Modules/Tournaments/TournamentEndpoints.cs` | Rutas |
| `Pactogg.Api/Modules/Tournaments/TournamentService.cs` | Servicio |
| `Pactogg.Api/Modules/Tournaments/Dtos/TournamentDtos.cs` | DTOs |
| `IntegrationTest/CoreFlowTests.cs` | Flujo/anidada |
| `api/openapi.yaml` | Contrato |

## Dependencias

MongoDB, Redis, autenticación JWT.

## Pruebas

Lista, bracket, anidada, grupo ajeno, vacío y filtros.

## Riesgos

IDOR de torneos y caché Redis sin prueba dedicada.

## Orden

1. DTO y validación.
2. Servicio y persistencia.
3. Endpoint y autorización.
4. Pruebas, OpenAPI y checklist.

## Cierre

Checklist, `Memoria.md` y `specs/validation.md`.

