# Tareas — 005 Contrato por voz con IA

[Spec](spec.md) · [plan](plan.md).

- [ ] 005-T1 — `IContractDraftProvider`, proveedor falso para pruebas y `UnavailableDraftProvider`; 005-R13; 005-AC11.
- [ ] 005-T2 — `DraftSchemaValidator` (esquema estricto, sin campos extra) con unitarias; 005-R5; 005-AC4, AC5.
- [ ] 005-T3 — `ContractDraftService`: membresía, longitud, apodos sin PII, instrucción con datos delimitados; 005-R1–R4; 005-AC1–AC3.
- [ ] 005-T4 — Emparejar rival, lista de miembros y `missing_fields` con reglas de CON-01 (ID 60, 59); 005-R6–R10; 005-AC1, AC6–AC8.
- [ ] 005-T5 — Límites 5/min y 20/día, caché de 10 min por hash, timeout y circuit breaker; 005-R11–R13; 005-AC9–AC11.
- [ ] 005-T6 — Ruta CON-08, OpenAPI y guía móvil; 005-R1, R2.
- [ ] 005-T7 — CON-01 con `source=voice` y `original_phrase`; 005-R14; 005-AC12.
- [ ] 005-T8 — Revisar logs sin frase; 005-R15; 005-AC13.
- [ ] 005-T9 — Proveedor real cuando el equipo entregue el modelo (Q-007) y clave en SSM (filas 73 y 82).
- [ ] 005-T10 — Prueba con 20 frases de ejemplo y ajuste del prompt (fila 74).

## Definición de terminado

AC1–AC13 en verde con el proveedor falso, Q-007 resuelta, prueba con 20 frases documentada, OpenAPI y guía móvil actualizados.
