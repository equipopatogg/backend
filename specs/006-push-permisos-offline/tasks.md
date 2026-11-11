# Tareas — 006 Push, permisos y modo sin conexión

[Spec](spec.md) · [plan](plan.md).

Backend (David M.):

- [x] 006-T1 — Registrar, quitar y preferencias de dispositivos (DEV-01..03); 006-R1, R4; 006-AC1.
- [x] 006-T2 — Centro de notificaciones (NOT-01..04) con TTL de 30 días; 006-R10, R11; 006-AC8, AC9.
- [x] 006-T3 — Push por FCM con cola, preferencias por categoría, deduplicación y borrado de tokens muertos (NOT-05); 006-R5–R9; 006-AC1, AC2, AC5–AC7.
- [x] 006-T4 — Tope de 5 dispositivos (006-D1); 006-R2; 006-AC3 (2026-11-06).
- [x] 006-T5 — Quitar dispositivos al cerrar sesión en todos lados, restablecer contraseña o detectar refresh robado; 006-R3; 006-AC4 (2026-11-06).
- [x] 006-T6 — Errores normales para acciones que ya no son válidas; 006-R14; 006-AC12.
- [ ] 006-T7 — Cuenta de servicio de Firebase en SSM y `user_data` (con SMTP; reemplaza instancias).
- [x] 006-T8 — `Idempotency-Key` opcional en CON-01 y EVI-02; 006-R13; 006-AC11.
- [x] 006-T9 — Aviso de compromiso por cumplir a los 7 días; 006-R15; 006-AC13.
- [x] 006-T10 — `updated_at` en grupo, miembro, compromiso y perfil; 006-R12; 006-AC10.

Móvil (Diego S. y José G.):

- [ ] 006-T11 — Parte móvil de la spec: permisos en contexto y manejo de negados (micrófono, notificaciones, cámara).
- [ ] 006-T12 — Room con la última copia del tablero, compromisos, miembros y perfil; WorkManager con 5 reintentos.
- [ ] 006-T13 — Registrar el token al iniciar sesión y llamar DEV-02 al cerrarla.

## Definición de terminado

AC1–AC13 en verde, push real probado en un celular, parte móvil aprobada.
