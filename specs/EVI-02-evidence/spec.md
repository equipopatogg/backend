# EVI-02 — POST /api/v1/contracts/{id}/evidence

**Fecha:** 2026-11-03
**Estado:** Implementado y verificado localmente; aceptación externa pendiente.
**IDs:** EVI-02, BE-14, BE-21, BE-32
**Contrato:** `api/openapi.yaml`

## 1. Contexto y objetivo

Adjuntar enlace o foto a un contrato, moderar antes de almacenar y limitar a tres evidencias.

## 2. Requisitos funcionales

- RF1: exigir usuario autenticado y contrato existente en estado que admita evidencia.
- RF2: si el objetivo es miembro, solo el retado; si es grupo, cualquier miembro activo.
- RF3: enlace JSON `type=link` y `url`; HTTPS, puerto por defecto, sin userinfo, host permitido.
- RF4: no descargar ni copiar el enlace a S3; moderar la URL como texto.
- RF5: foto multipart `type=photo` opcional y `file`; JPEG/PNG/WebP, máximo 5 MB.
- RF6: validar magic bytes, reencodificar a JPEG y eliminar metadatos con SkiaSharp.
- RF7: moderar la foto antes de S3; rechazo devuelve `422 CONTENT_REJECTED` sin escribir.
- RF8: guardar foto en `evidence/{group_id}/{contract_id}/{evidence_id}.jpg`, retención 30 días.
- RF9: máximo 3 evidencias. La primera evidencia aprobada pasa el contrato a `voting`; mientras no haya votos se aceptan más evidencias hasta 3 (CLARIFICACION #30, #59, #60).
- RF10: auditar `evidence.created`; revertir contador si falla el almacenamiento.

## 3. Requisitos no funcionales

Moderación previa obligatoria. Sin video. S3 privado. Fallo de moderación cerrado. Sin URLs firmadas en logs. Rate limit de 10 evidencias por minuto y usuario.

## 4. Contrato

### Request

Enlace:

```json
{ "type": "link", "url": "https://www.youtube.com/watch?v=abc123" }
```

Foto: `multipart/form-data` con `file` y `type=photo` opcional. Hosts permitidos: `youtube.com`, `youtu.be`, `twitch.tv`, `clips.twitch.tv`, `medal.tv`, `streamable.com`, con `www.`/`m.` normalizados.

### Response

`201 Created`:

```json
{
  "data": {
    "id": "…",
    "contract_id": "…",
    "user_id": "…",
    "type": "link",
    "url": "https://www.youtube.com/watch?v=abc123",
    "provider": "youtube",
    "object_key": null,
    "moderation": { "status": "approved", "labels": [], "moderated_at": "…" },
    "created_at": "…",
    "expires_at": null
  }
}
```

### Errores

| Código HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_CONTRACT_TARGET` | No es retado ni miembro |
| 404 | `NOT_FOUND` | Contrato inexistente |
| 409 | `INVALID_CONTRACT_STATE` | Estado no admite evidencia |
| 409 | `EVIDENCE_LIMIT_REACHED` | Más de 3 evidencias |
| 422 | `INVALID_EVIDENCE_URL` | Enlace inválido |
| 422 | `UNSUPPORTED_MEDIA_TYPE` | Imagen no permitida |
| 422 | `IMAGE_TOO_LARGE` | Más de 5 MB |
| 422 | `CONTENT_REJECTED` | Moderación rechaza |
| 503 | `MODERATION_UNAVAILABLE` | Moderación no disponible |
| 500 | `INTERNAL_ERROR` | Fallo seguro |

## 5. Arquitectura y decisiones

| Ruta | Responsabilidad |
|---|---|
| `Pactogg.Api/Modules/Evidence/EvidenceEndpoints.cs` | JSON y multipart |
| `Pactogg.Api/Modules/Evidence/EvidenceService.cs` | Autorización, moderación, S3 y límite |
| `Pactogg.Api/Modules/Evidence/EvidenceUrlValidator.cs` | Allowlist HTTPS |
| `Pactogg.Api/Modules/Evidence/ImageProcessor.cs` | Reencode JPEG |
| `Pactogg.Api/Infrastructure/Storage/S3ObjectStorage.cs` | S3 privado |

- **EVI02-D1:** una sola llamada multipart para foto (D-17).
- **EVI02-D2:** enlaces no se descargan ni se suben a S3 (D-18).
- **EVI02-D3:** transición a `voting` con la primera evidencia aprobada (Q-017 resuelta, F2-D20). Cuando exista VOT-02, el primer voto bloqueará nuevas evidencias.
- **EVI02-D4:** moderación antes de S3; fallo cerrado.
- **EVI02-D5 (F2-D22):** política de imágenes para videojuegos con Rekognition (`ImageModerationPolicy`, configurable en `Moderation:Image`). Confianza mínima 80 % (CLARIFICACION #63). Siempre se bloquea Explicit, Hate Symbols, Visually Disturbing y Self-Harm. `Graphic Violence` (sangre, explosiones) se bloquea en fotos reales y se permite si Rekognition marca el contenido como `Animated`/`Illustrated` (captura de juego) con confianza ≥ 70 %. `Weapons` y las demás categorías se permiten.

## 6. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| EVI02-AC1 | Enlace válido devuelve 201 | Flujo de integración |
| EVI02-AC2 | Enlaces no HTTPS/host no permitido devuelven 422 | `Evidence_RejectsNonHttpsAndUnknownHost` |
| EVI02-AC3 | Imagen inválida devuelve 422 | `Evidence_RejectsInvalidImageAndCapsAtThreeLinks` |
| EVI02-AC4 | Cuarta evidencia devuelve 409 | Misma prueba |
| EVI02-AC5 | Foto rechazada no escribe S3 | `FunctionalFlowChecks` |
| EVI02-AC6 | Claves NoSQL rechazadas | `Evidence_RejectsDangerousNoSqlKeys` |

## 7. Plan de pruebas

Enlace válido, URL insegura, imagen inválida, límite de 3, moderación rechazada sin S3 y NoSQL. La política de imágenes tiene pruebas unitarias (`ImageModerationPolicyTests`). Faltan pruebas de foto aprobada con S3 real y Rekognition real.

## 8. Riesgos y deuda

Bloquear evidencias tras el primer voto depende de VOT-02 (no implementado). S3 real no probado. La moderación de enlace es solo validación de URL. Fallo de S3 puede mapearse a 500 genérico.

## Aprobación

| Estado | Revisores | Fecha | Detalle |
|---|---|---|---|
| Implementación local | Agente | 2026-11-03 | Build y pruebas |
| Aceptación externa | Pendiente | Pendiente | AWS/TLS/revisión |

[Plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).

