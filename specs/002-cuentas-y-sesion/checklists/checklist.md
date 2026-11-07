# Validación — 002 Cuentas y sesión

- [x] Contraseñas con Argon2id; nunca en logs.
- [x] Respuesta genérica y mismo trabajo de hash en login (sin enumeración, F2-D51).
- [x] Bloqueo tras 5 fallos, también con Redis caído (F2-D53).
- [x] Sesión persistente con refresh rotativo, detección de reutilización y revocación.
- [x] Verificación de correo y recuperación con tokens de un solo uso hasheados.
- [x] Privacidad aceptada con versión vigente.
- [x] Apodo obligatorio, único (3–24) y moderado.
- [x] Google valida firma, emisor, audiencia, vigencia y correo verificado; falla cerrada.
- [x] Límite de peticiones en register/login/refresh/google/check-nickname/cuenta.
- [x] Spec completa según CLARIFICACION #138; cero preguntas abiertas.
- [x] Coherente con la constitución (§6.1 cuentas, §8 seguridad).
- [x] Aprobación del Product Owner (2026-11-06).
- [ ] Revisión técnica (Kevin) y comprobabilidad (José).
