# Tareas — 010 Torneos entre grupos

[Spec](spec.md) · [plan](plan.md).

- [x] 010-T0 — TOR-02 lista de torneos (v0.4).
- [x] 010-T1 — Dominio, código del torneo y `BracketBuilder` con byes; 010-R7; 010-AC3.
- [x] 010-T2 — Crear, detalle e inscripción con cobro e idempotencia; 010-R1, R2, R5; 010-AC1, AC8.
- [x] 010-T3 — Editar, salir, cancelar con devolución y cancelación automática; 010-R3, R4, R6, R16; 010-AC2, AC7. (R16 en v0.12.0: `MatchService.OnGroupDeletedAsync`.)
- [x] 010-T4 — Iniciar, bracket, partida y siguiente partida; 010-R7, R9, R15; 010-AC3, AC8.
- [x] 010-T5 — Reportar con foto, confirmar, disputar y resolver; 010-R10..R13; 010-AC4, AC5.
- [x] 010-T6 — Final, reparto de la bolsa y trofeo; 010-R14; 010-AC6.
- [x] 010-T7 — Invitar grupos y avisos `tournament_*`; 010-R8.
- [x] 010-T8 — OpenAPI y guía móvil. (Parte 1 en v0.11.0, parte 2 en v0.12.0.)

## Definición de terminado

AC1–AC8 en verde, OpenAPI actualizado, prueba manual de un torneo de 3 grupos en producción.

Estado 2026-11-12 (v0.12.0): T0 a T8 hechas; AC1–AC8 en verde. Solo falta la prueba manual de un torneo de 3 grupos en producción.
