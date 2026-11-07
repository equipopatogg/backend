# Tareas — 002 Cuentas y sesión

[Spec](spec.md) · [plan](plan.md).

- [x] 002-T1 — Registro con correo y privacidad (ID 11, 19); 002-R1, 002-R2, 002-R15; 002-AC1, 002-AC11.
- [x] 002-T2 — Login con bloqueo (ID 13); 002-R5, 002-R7; 002-AC1, 002-AC7.
- [x] 002-T3 — Refresh rotativo con reutilización y logout (ID 14, 15); 002-R8, 002-R9, 002-R10; 002-AC1, 002-AC8.
- [x] 002-T4 — Apodo único, check-nickname y avatar (ID 21, 22); 002-R13, 002-R14; 002-AC4, 002-AC10.
- [x] 002-T5 — `POST /auth/google` (ID 12) con validación real de Google; 002-R3, 002-R4; 002-AC2, 002-AC3.
- [x] 002-T6 — Verificación de correo y recuperación de contraseña (ID 17, 16); 002-R11, 002-R12; 002-AC9.
- [x] 002-T7 — Hash ficticio para correo inexistente (Q-020, F2-D51); 002-R6; 002-AC6 (2026-11-06).
- [x] 002-T8 — Bloqueo de login con respaldo local si Redis cae (F2-D53); 002-R7; 002-AC7 (2026-11-06).
- [ ] 002-T9 — Configurar `Auth:GoogleClientIds` y la credencial SMTP en producción, y activar `Auth:RequireVerifiedEmail` (acción del equipo).

## Definición de terminado

Pruebas en verde, OpenAPI y `docs/API-movil.md` actualizados.
