# Checklist — BE-00

[Spec](../spec.md) · [plan](../plan.md) · [tareas](../tasks.md).

| Criterio | Tareas | Estado | Evidencia / límite |
|---|---|---|---|
| BE00-AC1 | BE00-T1/T5 | PASS | Build Release 0 errores |
| BE00-AC2 | BE00-T3/T4/T6 | PASS local | Archivo local ignorado; conexión TLS e índices Atlas verificados |
| BE00-AC3 | BE00-T3/T6 | PASS local / NO_EJECUTADO AWS | `/health` 200 local |
| BE00-AC4 | BE00-T1 | PASS por código | Swagger solo Development/Testing; falta prueba Production |
| BE00-AC5 | BE00-T2/T5 | PASS | SecurityTest |

- [x] Configuración local sensible fuera de Git.
- [x] Middlewares y cabeceras cubiertos.
- [x] Build y pruebas ejecutados.
- [ ] E2E Atlas, AWS y TLS exterior.
- [x] Memoria y validación enlazadas.

