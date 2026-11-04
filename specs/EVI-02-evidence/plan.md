# Plan — EVI-02

[Spec](spec.md).

## Enfoque

Validar contrato/autorización, validar enlace o imagen, moderar, reservar cupo, almacenar si aplica y auditar con rollback.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Modules/Evidence/EvidenceEndpoints.cs` | Rutas |
| `Pactogg.Api/Modules/Evidence/EvidenceService.cs` | Servicio |
| `Pactogg.Api/Modules/Evidence/EvidenceUrlValidator.cs` | URL |
| `Pactogg.Api/Modules/Evidence/ImageProcessor.cs` | Imagen |
| `IntegrationTest/CoreFlowTests.cs` | Pruebas |
| `api/openapi.yaml` | Contrato |

## Dependencias

SkiaSharp, AWS Rekognition/Comprehend, S3, MongoDB, Redis.

## Pruebas

Enlace, URL insegura, imagen, límite, moderación y NoSQL.

## Riesgos

Transición a voting y S3/AWS reales; falta prueba de foto aprobada.

## Orden

1. DTO y validación.
2. Servicio y persistencia.
3. Endpoint y autorización.
4. Pruebas, OpenAPI y checklist.

## Cierre

Checklist, `Memoria.md` y `specs/validation.md`.

