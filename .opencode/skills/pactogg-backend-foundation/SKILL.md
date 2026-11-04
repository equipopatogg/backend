---
name: pactogg-backend-foundation
description: Revisa la base BE-00 de PactoGG al contrastar host, opciones, DI, middlewares, configuración local y diagnóstico.
---

# Base sin dependencias ficticias

Aplicar [AGENTS](../../../AGENTS.md), [constitución](../../../constitution.md) y [fase](../../../.specify/memory/constitution.md). Consultar [BE-00](../../../specs/BE-00-base-api/spec.md) y su plan.

- Contrastar host .NET 8, DI resoluble, opciones tipadas y carga opcional de `appsettings.{Environment}.local.json` ignorado por Git.
- Revisar orden de middlewares: request id, errores, cabeceras, NoSQL, CORS, autenticación, rate limit y autorización.
- Comprobar que `/health` es anónimo y que Swagger solo se expone en Development/Testing.
- No declarar salud de MongoDB/Redis si no hay check registrado. La conexión Atlas real y TLS exterior requieren evidencia separada.
- Los secretos nunca se escriben en `appsettings.json` ni se registran en logs.

Salida: criterio BE00-AC afectado, decisión abierta y evidencia positiva/negativa. No crear endpoints ni paquetes por imitación.

