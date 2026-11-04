# GRP-01 — POST /api/v1/groups

**Fecha:** 2026-11-03
**Estado:** Implementado y verificado localmente; aceptación externa pendiente.
**IDs:** GRP-01, BE-12, BE-33
**Contrato:** `api/openapi.yaml`

## 1. Contexto y objetivo

Crear un grupo privado, dejar al creador como admin con puntos iniciales y generar un código de invitación único.

## 2. Requisitos funcionales

- RF1: exigir usuario autenticado.
- RF2: aceptar `name` y `settings` opcional (`jurors`, `consensus`, `bond_options`).
- RF3: validar nombre 2–40, jurados 1–9, consenso 0.51–1.0 y 1–5 fianzas únicas entre 10 y 200.
- RF4: limitar a 5 grupos activos por usuario; exceso devuelve `409 GROUP_LIMIT_REACHED`.
- RF5: crear grupo con `invite_code` de 6 caracteres criptográfico y único.
- RF6: crear miembro `admin` activo con 100 puntos.
- RF7: si falla la creación del miembro, borrar el grupo y propagar el error.
- RF8: auditar `group.created`.

## 3. Requisitos no funcionales

Autorización obligatoria. Código de invitación sin caracteres ambiguos. Sin PII en logs. La creación no debe dejar grupos huérfanos.

## 4. Contrato

### Request

```json
{
  "name": "Los Compas",
  "settings": {
    "jurors": 3,
    "consensus": 0.66,
    "bond_options": [25, 50, 100]
  }
}
```

### Response

`201 Created`:

```json
{
  "data": {
    "id": "…",
    "name": "Los Compas",
    "owner_id": "…",
    "invite_code": "K7M2QX",
    "invite_active": true,
    "member_count": 1,
    "settings": { "jurors": 3, "consensus": 0.66, "bond_options": [25, 50, 100] },
    "my_role": "admin",
    "my_points": 100,
    "created_at": "…"
  }
}
```

### Errores

| Código HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 409 | `GROUP_LIMIT_REACHED` | Más de 5 grupos activos |
| 422 | `VALIDATION_ERROR` | Nombre o settings inválidos |
| 503 | `INTERNAL_ERROR` | No se pudo generar código |
| 500 | `INTERNAL_ERROR` | Fallo inesperado seguro |

## 5. Arquitectura y decisiones

| Ruta | Responsabilidad |
|---|---|
| `Pactogg.Api/Modules/Groups/GroupEndpoints.cs` | Ruta `POST /groups` |
| `Pactogg.Api/Modules/Groups/GroupService.cs` | Reglas, código, admin y rollback |
| `Pactogg.Api/Modules/Groups/Dtos/GroupDtos.cs` | DTOs |
| `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoGroupRepository.cs` | Persistencia |

- **GRP01-D1:** máximo 5 grupos (D-22/Config).
- **GRP01-D2:** creador admin con 100 puntos.
- **GRP01-D3:** nombre 2–40 implementado; el texto histórico 3–30 queda pendiente (Q-019).
- **GRP01-D4:** la moderación del nombre no está implementada; deuda Q-021.

## 6. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| GRP01-AC1 | Creación válida devuelve 201 y admin | Flujo de integración |
| GRP01-AC2 | Sin token devuelve 401 | SecurityTest |
| GRP01-AC3 | Settings inválidos devuelven 422 | Código; falta prueba dedicada |
| GRP01-AC4 | Límite de 5 grupos devuelve 409 | Código; falta prueba dedicada |
| GRP01-AC5 | Código único y rollback | Revisión de servicio; falta prueba de concurrencia |

## 7. Plan de pruebas

Flujo de creación y uso del grupo en IntegrationTest; 401 en SecurityTest. Faltan pruebas de settings inválidos, límite y moderación de nombre.

## 8. Riesgos y deuda

Nombre sin moderación. Sin prueba de límite. Rollback no probado con fallo inducido. Invite code colisión manejada con reintentos.

## Aprobación

| Estado | Revisores | Fecha | Detalle |
|---|---|---|---|
| Implementación local | Agente | 2026-11-03 | Build y pruebas |
| Aceptación externa | Pendiente | Pendiente | AWS/TLS/revisión |

[Plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).

