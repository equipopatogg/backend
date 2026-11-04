---
name: pactogg-backend-endpoint-slices
description: Revisa que cada endpoint PactoGG tenga spec, plan, tareas, checklist, OpenAPI, pruebas y control de abuso.
---

# Un expediente por endpoint

Aplicar [AGENTS](../../../AGENTS.md), [constitución](../../../constitution.md) y [fase](../../../.specify/memory/constitution.md). Usar el [índice](../../../specs/README.md).

- Cada endpoint de evaluación tiene carpeta propia con `spec.md`, `plan.md`, `tasks.md` y `checklists/checklist.md`.
- La spec debe declarar método, ruta, request, response, errores, autorización, rate limit, moderación y criterios verificables.
- Las rutas alias se documentan en el expediente principal: `/contracts` y `/groups/{gid}/contracts`; `/tournaments` y `/groups/{gid}/tournaments`.
- Toda consulta filtra por `group_id` y pertenencia. Toda escritura valida el recurso y el rol.
- La evidencia cita pruebas locales, OpenAPI y límites externos. No declarar PASS de AWS, TLS o proveedores sin ejecución.

Salida: endpoint, criterio, evidencia y deuda. No crear endpoints nuevos ni duplicar el contrato en otro documento.

