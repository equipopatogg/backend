# Validación SDD — 001 a 003

**Fecha:** 2026-11-06 · **Fila del Sprint Planning:** 58 · **Responsable:** David M. (PO)
**Criterio:** CLARIFICACION #138 (spec completa) y #139 (funcionalidad aprobada); constitución §9.3 y §11.

## 1. Resultado por criterio de spec completa (#138)

Estado **antes** de la validación → **después**.

| Criterio | 001 Infraestructura | 002 Cuentas y sesión | 003 Grupos |
|---|---|---|---|
| Contexto | PASS → PASS | PASS → PASS | PASS → PASS |
| Fuera de alcance | FAIL → PASS | FAIL → PASS | FAIL (solo en riesgos) → PASS |
| Requisitos funcionales en EARS comprobables | FAIL (RF en prosa) → PASS (001-R1..R10) | FAIL (tabla de reglas) → PASS (002-R1..R16) | FAIL (tabla de reglas) → PASS (003-R1..R11) |
| Requisitos no funcionales | PASS → PASS | PASS → PASS | PASS → PASS |
| Endpoints con IDs del Excel | PASS → PASS | FAIL (sin IDs del CRUD) → PASS | FAIL (sin IDs del CRUD) → PASS |
| Esquemas | PASS (OpenAPI) → PASS | FAIL (sin nombres) → PASS | FAIL (sin nombres) → PASS |
| Criterios de aceptación por requisito | FAIL (sin relación con RF) → PASS | FAIL (R6, R7 sin prueba) → PASS | FAIL (R2, R3, R5 sin prueba) → PASS |
| Cero preguntas abiertas | PASS → PASS | FAIL (Q-019, Q-020) → PASS | FAIL (Q-019, Q-021) → PASS |
| Verificación contra la constitución | FAIL (solo una línea) → PASS | FAIL (solo una línea) → PASS | FAIL (solo una línea) → PASS |
| Aprobación del Product Owner | FAIL → PASS (2026-11-06) | FAIL → PASS (2026-11-06) | FAIL → PASS (2026-11-06) |

## 2. Decisiones del PO tomadas durante la validación

| Pregunta | Decisión | Registro |
|---|---|---|
| Q-019 Longitudes | Se quedan las del código: apodo 3–24, grupo 2–40 | F2-D50 |
| Q-020 Tiempo del login | Hash Argon2id ficticio con correo inexistente | F2-D51 |
| Q-021 Nombre del grupo | Se modera antes de guardarse, falla cerrada | F2-D52 |

## 3. Defectos encontrados al validar

| Defecto | Spec | Corrección | Prueba |
|---|---|---|---|
| Con Redis caído, el bloqueo tras 5 fallos dejaba de funcionar (violaba §8 "límites locales de respaldo") | 002-R7 | Contador local de respaldo en `RedisLoginAttemptStore` (F2-D53) | `SddDecisionTests.Login_AfterFiveFailures_IsTemporarilyLocked` |
| `/health` no revisaba la base (CLARIFICACION #136) | 001-R10 | `/health/ready` con ping a MongoDB; `/health` queda como vida del proceso para Caddy (F2-D54) | `HealthWithoutDatabaseTests.HealthReady_WhenDatabaseIsDown_Returns503` |
| Criterios sin prueba dedicada: 5 grupos, jurados inválidos, longitudes | 003-R1, R3, R5 | Pruebas nuevas | `SddDecisionTests` |

## 4. Resultado por criterio de funcionalidad aprobada (#139)

| Criterio | 001 | 002 | 003 |
|---|---|---|---|
| Criterios de aceptación cumplidos | PASS salvo 001-AC6 y 001-AC8 (acciones manuales) | PASS | PASS |
| Pruebas en verde | PASS (114 de integración en local) | PASS | PASS |
| Escaneos sin hallazgos críticos | PASS (run #10 del PR; el run de `main` con estos cambios falta) | PASS | PASS |
| Existe validación | PASS (este documento y `validation.md`) | PASS | PASS |
| Revisó otra persona | PENDIENTE (Kevin, José) | PENDIENTE | PENDIENTE |
| Aprobó el PO | PASS | PASS | PASS |

## 5. Pendientes para cerrar del todo

1. Revisión técnica de Kevin y de comprobabilidad de José en las tres specs (tabla de aprobación de cada `spec.md`).
2. 001-T9, 001-T10 y 001-T11 (acciones de infraestructura del equipo).
3. 002-T9: Client ID de Google, credencial SMTP y `Auth:RequireVerifiedEmail=true` en producción.
