# Validación — 005 Contrato por voz con IA

Spec (antes de implementar):

- [x] Contexto, fuera de alcance, EARS (005-R1..R15), RNF, endpoint CON-08 con esquemas y errores.
- [x] Criterio de aceptación por cada requisito (005-AC1..AC13).
- [x] Sin audio al backend; sin PII al proveedor; la IA no inventa rivales; la persona siempre revisa.
- [x] Prompt injection, salida no confiable, límites, timeout, circuit breaker y fallback manual.
- [x] Clave del proveedor en SSM; frase fuera de logs.
- [x] Verificación contra la constitución.
- [ ] Q-007 resuelta (proveedor de IA).
- [x] Aprobación del Product Owner (2026-11-06).
- [ ] Revisión técnica (Kevin) y comprobabilidad (José).

Implementación (después):

- [x] AC1–AC13 en verde con el proveedor falso (v0.12.0, 2026-11-12).
- [ ] Prueba con 20 frases documentada.
