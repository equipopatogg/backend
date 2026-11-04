---
name: pactogg-backend-api-boundaries
description: Revisa fronteras DTO, validación, errores, envoltura JSON y autorización de los endpoints PactoGG.
---

# Fronteras verificables

Aplicar [AGENTS](../../../AGENTS.md), [constitución](../../../constitution.md) y [fase](../../../.specify/memory/constitution.md). Consultar [BE-02](../../../specs/BE-02-api-boundaries/spec.md) y el expediente del endpoint.

- Verificar snake_case, rechazo de miembros desconocidos, envoltura `data`/`meta` y errores `code`/`message`/`request_id`/`details`.
- Revisar DataAnnotations y validaciones de servicio; una entrada inválida no debe invocar persistencia ni almacenamiento.
- Confirmar autorización por recurso y grupo antes de crear o leer. No confiar en `group_id` del cliente sin verificar pertenencia.
- Diferenciar error de entrada, conflicto, no autorizado, prohibido y fallo de infraestructura. Sin stack traces ni PII.
- OpenAPI es el contrato; una fixture o un test no aprueban campos de negocio.

Salida: criterio afectado, caso positivo/negativo y evidencia. No inventar campos ni códigos.

