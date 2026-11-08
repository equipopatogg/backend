# Plan — 009 Compromisos, landing y Google Play

[Spec](spec.md).

## Enfoque

Backend: compromisos ya existen; solo falta el aviso a los 7 días dentro de `ContractLifecycleService` (misma tarea que 006-T9). Landing y Google Play los lleva Kevin fuera de este repositorio.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Modules/Contracts/Lifecycle/ContractLifecycleService.cs` | Aviso `commitment_overdue` |
| Repositorio de la landing (Kevin) | Sitio, aviso de privacidad, Analytics |
| Play Console (Kevin) | Canal de pruebas internas |
