# Plan — 008 Jurado, puntos y resultados

[Spec](spec.md).

## Enfoque

Jurado, votación, plazos, libro de puntos y ranking ya existen (v0.6–v0.9). Faltan el cálculo del Índice de Palabra, el Muro de la Vergüenza y los logros. El Índice se calcula con una función pura (`WordIndexCalculator`) sobre contadores que se actualizan en el mismo punto donde se cierra cada reto, compromiso o voto.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Modules/Reputation/WordIndexCalculator.cs` | Fórmula §6.8 |
| `Pactogg.Api/Modules/Auth/Domain/User.cs` (`UserStats`) | Contadores: vencidos, compromisos a tiempo y vencidos, votos alineados |
| `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoContractRepository.cs` | Actualizar contadores al resolver o vencer |
| `Pactogg.Api/Modules/Fame/FameService.cs` | ACH-02 Muro de la Vergüenza |
| `Pactogg.Api/Modules/Achievements/*` + colección `achievements` | ACH-04 y catálogo v1 |
| `database/mongo-init.js`, `MongoIndexInitializer.cs` | Colección e índices de logros |
| `UnitTest/WordIndexCalculatorTests.cs`, `IntegrationTest/ReputationTests.cs` | Pruebas 008-AC7..AC9 |

## Riesgos

Los contadores viejos empiezan en 0: el Índice de cuentas existentes solo refleja eventos desde el despliegue (se documenta).
