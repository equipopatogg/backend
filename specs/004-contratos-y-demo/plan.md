# Plan — 004 Contratos y demo

[Spec](spec.md).

## Enfoque

El código de contratos se adelantó en las versiones 0.5–0.8; esta spec se escribió después y se verificó contra él (trazabilidad requisito → prueba). Lo que faltaba se agregó con pruebas: moderación del juego y criterios sin prueba dedicada. La demo no toca el backend.

## Diseño

- `ContractService` aplica reglas de creación, edición, aceptación y rechazo; `ContractRules` normaliza la recompensa.
- `MongoContractRepository` hace las transiciones con versión esperada y transacción (estado + `group_members` + `points_ledger`).
- `ContractLifecycleService` + `ContractDeadlineWorker` (cada 5 min) vencen retos y cierran votaciones.
- `NotificationService.NotifyAsync` crea el aviso `contract_received` con clave de deduplicación.
- `IdempotencyMiddleware` protege `accept`.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Modules/Contracts/*` | Endpoints, reglas, DTO, ciclo de vida |
| `Pactogg.Api/Modules/Contracts/ContractService.cs` | Moderación del juego al crear y editar (004-T7) |
| `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoContractRepository.cs` | Transiciones atómicas |
| `Pactogg.Api/Modules/Audit/AuditModule.cs` | Historial del grupo (AUD-01) |
| `IntegrationTest/Spec004ContractTests.cs` | Pruebas nuevas 004-AC4, AC5, AC7, AC8 |

## Dependencias

Moderación de texto (Comprehend o local), Redis para idempotencia y caché, MongoDB con transacciones (Atlas replica set).

## Riesgos

Comprehend solo en inglés; 004-T8 bloqueada por MEM-04/05.

## Orden

1. Verificar R1–R15 contra el código y las pruebas. 2. Cerrar huecos (juego moderado, pruebas). 3. Documentar Q-022. 4. OpenAPI.
