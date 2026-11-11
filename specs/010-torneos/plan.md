# Plan — 010 Torneos entre grupos

[Spec](spec.md).

## Enfoque

Ampliar `Modules/Tournaments` (hoy solo TOR-02) con un servicio de torneos y uno de partidas. El bracket se genera con una función pura (`BracketBuilder`) probada con 2 a 16 grupos. Inscripción, salida, cancelación y premios usan transacciones y movimientos del libro como las fianzas.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Modules/Tournaments/TournamentService.cs`, `TournamentEndpoints.cs` | TOR-01, TOR-03..09 |
| `Pactogg.Api/Modules/Tournaments/MatchService.cs`, `MatchEndpoints.cs` | MAT-01..07 |
| `Pactogg.Api/Modules/Tournaments/BracketBuilder.cs` | Sorteo, byes y enlaces |
| `Pactogg.Api/Modules/Tournaments/Domain/*` | Tamaño, código, representantes, motivo de disputa |
| Repositorios Mongo + fakes de torneos y partidas | Transacciones e índices |
| `database/mongo-init.js`, `MongoIndexInitializer.cs` | Índices `ux_tournaments_code`, `ix_matches_tournament` |
| `UnitTest/BracketBuilderTests.cs`, `IntegrationTest/TournamentFlowTests.cs` | 010-AC1..AC8 |
| `api/openapi.yaml`, `docs/API-movil.md` | Contrato |

## Orden

1. Dominio y `BracketBuilder` con unitarias. 2. TOR-01, TOR-03, TOR-06, TOR-07. 3. TOR-08 y MAT-01, MAT-02, MAT-07. 4. MAT-03..06 y premio. 5. TOR-04, TOR-05, TOR-09 y cancelación automática. 6. OpenAPI.

## Riesgos

Es el bloque más grande (15 endpoints); la foto del reporte reutiliza la moderación de EVI-02.
