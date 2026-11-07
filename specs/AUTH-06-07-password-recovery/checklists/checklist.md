# Validación — AUTH-06 / AUTH-07 — Recuperar contraseña y cambio de contraseña

- [x] Pública con límite de peticiones por IP (sin datos de otras cuentas en la respuesta).
- [x] Respuesta genérica: no revela si una cuenta existe.
- [x] Valida la entrada; campos desconocidos y claves NoSQL peligrosas se rechazan (400/422).
- [x] Errores con `code`, `message` y `request_id`; sin datos sensibles en logs.
- [x] Documentado en `api/openapi.yaml`.
- [x] Pruebas en verde: `AccountFlowTests.ForgotAndReset_AreGeneric_SingleUse_AndRevokeSessions`, `AccountFlowTests.ChangePassword_RequiresCurrent_AndReturnsFreshSession`.
- [x] Criterios de aceptación cubiertos: los de la spec.
- [x] Coherente con la constitución (§3, §7, §8).
- [ ] Revisión técnica (Kevin) y comprobabilidad (José).
