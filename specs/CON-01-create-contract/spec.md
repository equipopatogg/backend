# CON-01 — POST /api/v1/contracts

**Fecha:** 2026-11-03
**Estado:** Implementado y verificado localmente; aceptación externa pendiente.
**IDs:** CON-01, BE-12, BE-14, BE-21
**Contrato:** `api/openapi.yaml`

## 1. Contexto y objetivo

Crear un contrato `open` dentro de un grupo, con moderación de texto, fianza permitida y objetivo validado.

## 2. Requisitos funcionales

- RF1: exigir usuario autenticado y miembro activo del grupo.
- RF2: aceptar ruta plana `POST /contracts` con `group_id` y alias `POST /groups/{gid}/contracts`.
- RF3: exigir `group_id`, `game`, `condition`, `reward`, `deadline`, `bond_required`, `target_type`, `target_id` y `source`.
- RF4: `deadline` futura; `bond_required` dentro de `group.settings.bond_options`.
- RF5: `source` solo `manual`.
- RF6: `target_type` `group` o `member`; si es `member`, `target_id` debe ser miembro del grupo.
- RF7: recompensa 3–80 sin dinero ni símbolos monetarios.
- RF8: moderar `condition` y `reward`; rechazo devuelve `422 CONTENT_REJECTED`.
- RF9: crear contrato `open`, versión 1, evidencia 0 y auditar `contract.created`.

## 3. Requisitos no funcionales

Anti-IDOR por pertenencia. No se procesa dinero. La recompensa es virtual. Sin PII en logs. La ruta anidada no permite `group_id` contradictorio.

## 4. Contrato

### Request

```json
{
  "group_id": "…",
  "game": "Valorant",
  "condition": "Ganar tres partidas seguidas en ranked",
  "reward": "Elegir el próximo juego del grupo",
  "deadline": "2026-11-10T23:59:00Z",
  "bond_required": 50,
  "target_type": "group",
  "target_id": null,
  "source": "manual"
}
```

En ruta anidada, `group_id` se toma de `{gid}` y un valor distinto en el cuerpo devuelve 422.

### Response

`201 Created` con `id`, `group_id`, `challenger_id`, `target_type`, `target_id`, `game`, `condition`, `reward`, `deadline`, `bond_required`, `status=open`, `version=1`, `created_at`.

### Errores

| Código HTTP | `code` | Causa |
|---|---|---|
| 401 | `UNAUTHORIZED` | Sin token |
| 403 | `NOT_GROUP_MEMBER` | No pertenece al grupo |
| 404 | `NOT_FOUND` | Grupo inexistente |
| 422 | `VALIDATION_ERROR` | Datos, fecha o `source` inválidos |
| 422 | `INVALID_BOND` | Fianza no permitida |
| 422 | `INVALID_TARGET` | Objetivo inválido |
| 422 | `CONTENT_REJECTED` | Moderación rechaza |
| 500 | `INTERNAL_ERROR` | Fallo inesperado seguro |

## 5. Arquitectura y decisiones

| Ruta | Responsabilidad |
|---|---|
| `Pactogg.Api/Modules/Contracts/ContractEndpoints.cs` | Rutas plana y anidada |
| `Pactogg.Api/Modules/Contracts/ContractService.cs` | Reglas y creación |
| `Pactogg.Api/Modules/Contracts/ContractRules.cs` | Recompensa |
| `Pactogg.Api/Modules/Contracts/Dtos/ContractDtos.cs` | DTOs |

- **CON01-D1:** ruta plana y alias anidado (D-14).
- **CON01-D2:** `target_type` es `group|member` (Q-018 resuelta, F2-D21; CLARIFICACION #32).
- **CON01-D3:** retarse a sí mismo devuelve `422 INVALID_TARGET` (F2-D21). Prueba `Contract_TargetingYourself_Returns422`.
- **CON01-D4:** moderación de condición y recompensa antes de persistir.

## 6. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| CON01-AC1 | Creación válida devuelve 201 | Flujo de integración |
| CON01-AC2 | No miembro devuelve 403 | `Contract_FromNonMember_ReturnsForbidden` |
| CON01-AC3 | Ruta anidada y `group_id` contradictorio | `NestedContractRoute_CreatesContractAndRejectsMismatchedGroup` |
| CON01-AC4 | Recompensa con dinero se rechaza | UnitTest `ContractRulesTests` |
| CON01-AC5 | Moderación rechaza condición | Fake de moderación; falta prueba dedicada de contrato |

## 7. Plan de pruebas

Flujo de contrato, 403 de no miembro, ruta anidada y reglas de recompensa. Auto-reto cubierto. Faltan pruebas de fianza inválida y objetivo inválido.

## 8. Riesgos y deuda

Auto-reto permitido. `target_type` difiere del texto histórico. Sin prueba de fianza/objetivo. La moderación de texto depende del adaptador local/AWS.

## Aprobación

| Estado | Revisores | Fecha | Detalle |
|---|---|---|---|
| Implementación local | Agente | 2026-11-03 | Build y pruebas |
| Aceptación externa | Pendiente | Pendiente | AWS/TLS/revisión |

[Plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).

