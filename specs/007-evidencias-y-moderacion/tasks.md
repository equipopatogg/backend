# Tareas — 007 Evidencias y moderación

[Spec](spec.md) · [plan](plan.md).

- [x] 007-T1 — EVI-02 foto y enlace con moderación previa, límite de 3 y paso a `voting`; 007-R1..R5; 007-AC1..AC3.
- [x] 007-T2 — EVI-03 solo aprobadas con URL del CDN; 007-R6; 007-AC4.
- [x] 007-T3 — EVI-04 reemplazar antes del primer voto; 007-R7; 007-AC5.
- [x] 007-T4 — EVI-05 borrar (autor antes del primer voto); 007-R8; 007-AC6.
- [x] 007-T5 — EVI-06 guardar en el Muro de la Fama; 007-R9; 007-AC7.
- [x] 007-T6 — Borrado a los 30 días fuera de la Fama; 007-R10; 007-AC8.
- [x] 007-T7 — Lista local de términos en español; 007-R11; 007-AC9.
- [x] 007-T8 — OpenAPI y guía móvil.

## Definición de terminado

AC1–AC9 en verde, `terraform plan` sin reemplazo de instancias, OpenAPI actualizado.

Estado 2026-11-10 (v0.10.0): T3–T8 hechas con pruebas en verde. Falta correr `terraform plan`/`apply` para crear la regla del bucket (solo toca S3, no recrea instancias).
