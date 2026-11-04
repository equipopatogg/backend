# TOR-02 — GET /api/v1/tournaments

**Fecha:** 2026-11-03
**Estado:** Implementado y verificado localmente; aceptación externa pendiente.
**IDs:** TOR-02, TOR-03, BE-14, BE-31
**Contrato:** `api/openapi.yaml`

## 1. Contexto y objetivo

Listar torneos de los grupos activos del usuario con filtros, paginación por cursor y bracket.

## 2. Requisitos funcionales

- RF1: exigir JWT.
- RF2: aceptar filtros `status`, `game`, `group_id`, `limit` y `cursor`.
- RF3: `status` en `open|in_progress|finished|cancelled`; `limit` 1–50, default 20; `game` máximo 60.
- RF4: si se envía `group_id`, verificar pertenencia; si no, usar todos los grupos activos.
- RF5: sin grupos activos devolver `200` con arreglo vacío.
- RF6: incluir participantes, bolsa, estado y partidas del bracket.
- RF7: cachear 30 segundos por usuario y filtros.
- RF8: alias `GET /api/v1/groups/{gid}/tournaments`.

## 3. Requisitos no funcionales

Anti-IDOR por grupo. No exponer torneos de grupos ajenos. Caché aislada por usuario. Sin datos personales en logs.

## 4. Contrato

### Request

Query opcional: `status`, `game`, `group_id`, `limit=20`, `cursor`. Ejemplo: `GET /api/v1/tournaments?status=open&limit=20`.

### Response

`200 OK`:

```json
{
  "data": [
    {
      "id": "…",
      "group_id": "…",
      "game": "Valorant",
      "format": "single_elimination",
      "entry_points": 0,
      "prize_pool": 100,
      "scheduled_at": "…",
      "participants": [],
      "status": "open",
      "winner_group_id": null,
      "bracket": { "matches": [] }
    }
  ],
  "meta": { "next_cursor": null, "has_more": false }
}
```

### Errores

| Código HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_GROUP_MEMBER` | `group_id` ajeno |
| 422 | `VALIDATION_ERROR` | Filtros, límite o cursor inválidos |
| 500 | `INTERNAL_ERROR` | Fallo inesperado seguro |

## 5. Arquitectura y decisiones

| Ruta | Responsabilidad |
|---|---|
| `Pactogg.Api/Modules/Tournaments/TournamentEndpoints.cs` | Rutas plana/anidada |
| `Pactogg.Api/Modules/Tournaments/TournamentService.cs` | Filtros, caché y bracket |
| `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoTournamentRepository.cs` | Consulta |
| `Pactogg.Api/Infrastructure/Redis/RedisCacheService.cs` | Caché 30 s |

- **TOR02-D1:** ruta plana y alias anidado (D-20).
- **TOR02-D2:** por defecto solo grupos activos del usuario.
- **TOR02-D3:** `group_id` ajeno devuelve 403.
- **TOR02-D4:** caché 30 s por usuario/filtros; fallback local si Redis no está.

## 6. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| TOR02-AC1 | Lista de torneos del usuario | Flujo de integración |
| TOR02-AC2 | Ruta anidada filtra por grupo | `NestedTournamentsRoute_ReturnsOnlyRequestedGroup` |
| TOR02-AC3 | Grupo ajeno devuelve 403 | Código; falta prueba dedicada |
| TOR02-AC4 | Sin grupos devuelve 200 vacío | Código; falta prueba dedicada |
| TOR02-AC5 | Bracket incluido | Flujo de integración |
| TOR02-AC6 | Filtros inválidos devuelven 422 | Código; falta prueba dedicada |

## 7. Plan de pruebas

Flujo de lista y bracket, ruta anidada y 401. Faltan pruebas de grupo ajeno, lista vacía, cursor y filtros inválidos.

## 8. Riesgos y deuda

Caché Redis real no probada. Faltan pruebas de IDOR de torneos. El cursor es ObjectId, no un cursor opaco.

## Aprobación

| Estado | Revisores | Fecha | Detalle |
|---|---|---|---|
| Implementación local | Agente | 2026-11-03 | Build y pruebas |
| Aceptación externa | Pendiente | Pendiente | AWS/TLS/revisión |

[Plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).

