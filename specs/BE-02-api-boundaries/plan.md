# Plan — BE-02

[Spec](spec.md).

## Enfoque

Convenciones compartidas de serialización, errores, validación y claims; cada endpoint declara DTOs explícitos.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Common/` | Resultados, errores, filtros y middlewares |
| `Pactogg.Api/Modules/*/Dtos/` | DTOs de endpoints |
| `api/openapi.yaml` | Contrato |

## Dependencias

System.Text.Json, DataAnnotations, JWT Bearer, FluentAssertions en pruebas.

## Pruebas

Campo desconocido, NoSQL, 401/403, cabeceras, OpenAPI y flujos funcionales.

## Riesgos

Divergencia entre OpenAPI y DTOs; errores de infraestructura; consumidor Android no probado.

## Orden

1. Convenciones comunes.
2. DTOs y validación.
3. Pruebas y OpenAPI.

## Cierre

Checklist y validación.

