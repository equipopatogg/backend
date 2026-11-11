# Plan — 009 Compromisos, landing y Google Play

[Spec](spec.md).

## Enfoque

Backend: compromisos ya existen; solo falta el aviso a los 7 días dentro de `ContractLifecycleService` (misma tarea que 006-T9). Google Play lo lleva Kevin; la landing ya está hecha y no entra aquí.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Modules/Contracts/Lifecycle/ContractLifecycleService.cs` | Aviso `commitment_overdue` |
| Play Console (Kevin) | Canal de pruebas internas |
