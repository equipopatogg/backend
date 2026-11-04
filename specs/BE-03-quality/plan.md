# Plan — BE-03

[Spec](spec.md).

## Enfoque

Ejecutar y registrar evidencia local; declarar límites externos sin simular PASS.

## Archivos

| Ruta | Cambio |
|---|---|
| `UnitTest/` | Reglas y flujo |
| `IntegrationTest/` | Flujo y fakes |
| `SecurityTest/` | Abuso |
| `ContractTest/` | OpenAPI |
| `DatabaseTest/` | Índices Testcontainers |
| `MutationTest/` | Stryker |
| `PerformanceTest/` | NBomber |
| `specs/validation.md` | Resultados |

## Dependencias

SDK .NET 8, xUnit, FluentAssertions, Testcontainers, Stryker, NBomber, Trivy/NuGet.

## Pruebas

Build, suites, SCA, Terraform. Docker y AWS solo cuando existan.

## Riesgos

Falso verde por omisión, tests acoplados a fakes, falta de cobertura, Docker/AWS no disponibles.

## Orden

1. Build.
2. Suites.
3. SCA.
4. Terraform.
5. Validación.

## Cierre

`specs/validation.md` y `Memoria.md`.

