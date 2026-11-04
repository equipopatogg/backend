# BE-03 — Pruebas, SCA y coherencia de calidad

**Fecha:** 2026-11-03
**Estado:** Verificación local y build Docker ejecutados; AWS pendiente.
**IDs:** BE-02, BE-03, BE-21, BE-32

## 1. Contexto y objetivo

Evitar verde por informes ausentes. Consolidar build, pruebas, SCA, validación Terraform y checklist por endpoint.

## 2. Requisitos funcionales

- RF1: build Release sin errores.
- RF2: pruebas unitarias, integración, seguridad, contrato, mutación, rendimiento y base de datos.
- RF3: SCA sin vulnerabilidades conocidas en los ocho proyectos.
- RF4: Terraform fmt/validate correctos.
- RF5: cada endpoint con checklist y evidencia.
- RF6: Docker/Testcontainers/AWS declarados NO_EJECUTADOS si faltan.

## 3. Requisitos no funcionales

Reportes reproducibles, sin secretos, sin artefactos en Git. Una prueba omitida no es PASS. Los resultados locales no acreditan CI remoto.

## 4. Arquitectura y decisiones

| Ruta | Responsabilidad |
|---|---|
| `UnitTest/` | Reglas puras |
| `IntegrationTest/` | Flujo y fakes |
| `SecurityTest/` | Abuso, IDOR, cabeceras |
| `ContractTest/` | OpenAPI |
| `DatabaseTest/` | Testcontainers |
| `MutationTest/` | Stryker |
| `PerformanceTest/` | NBomber |
| `specs/validation.md` | Resumen |

- **BE03-D1:** xUnit + FluentAssertions.
- **BE03-D2:** SCA con NuGet vulnerable y Trivy.
- **BE03-D3:** DatabaseTest se omite sin Docker.
- **BE03-D4:** sin umbral de cobertura aprobado.

## 5. Criterios de aceptación

| ID | Escenario | Evidencia |
|---|---|---|
| BE03-AC1 | Build Release | 0 errores |
| BE03-AC2 | Suites verdes | 49 PASS, 1 omitida |
| BE03-AC3 | SCA limpio | Sin paquetes vulnerables |
| BE03-AC4 | Terraform válido | fmt/validate PASS |
| BE03-AC5 | Docker build / AWS | Docker PASS; AWS NO_EJECUTADO |

## 6. Plan de pruebas

Ejecutar build, siete suites, SCA, Terraform fmt/validate y build de imagen. En CI repetir con Testcontainers y escaneo de imagen. Documentar omisiones y bloqueos.

## 7. Riesgos y deuda

Cobertura no medida, mutación no ejecutada, integración WSL de Docker pendiente, AWS sin validar y Atlas verificado solo por conexión manual. Los tests en memoria no prueban Mongo/Redis reales.

## Aprobación

| Estado | Revisores | Fecha | Detalle |
|---|---|---|---|
| Verificación local | Agente | 2026-11-03 | Build/tests/SCA/Terraform |
| Aceptación remota | Pendiente | Pendiente | CI/Docker/AWS |

[Plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).

