# Plan — 006 Push, permisos y modo sin conexión

[Spec](spec.md).

## Enfoque

Backend: el push y el centro de notificaciones ya existen (0.6.0 y 0.8.0); esta spec los formaliza y cierra los huecos encontrados al escribirla (límite de 5 dispositivos y limpieza al cerrar sesión en todos lados). Lo que falta para el modo sin conexión (idempotencia opcional al crear y `updated_at` en más recursos) y el recordatorio de compromisos quedan como tareas. Móvil: Diego S. agrega permisos, Room y WorkManager en la parte móvil.

## Diseño (backend)

- `NotificationService.NotifyAsync` → `INotificationRepository.CreateManyAsync` (índice único parcial de `dedupe_key`) → `PushQueue` → `PushDispatcher` → `FcmPushSender`.
- `PushPreferences.Allows(type, prefs)` decide por prefijo del tipo.
- `DeviceService.RegisterAsync` aplica el tope de 5 (006-D1).
- `AuthService` (logout `all=true`, refresh robado) y `AccountService` (restablecer contraseña) llaman `IDeviceRepository.DeleteAsync(userId, null)`.
- 006-T8: permitir `Idempotency-Key` opcional en CON-01 y EVI-02 (nuevo metadato `AllowIdempotencyKey` en `IdempotencyMiddleware`: si viene la clave se aplica igual que en las obligatorias; si no, pasa).
- 006-T9: en `ContractLifecycleService.RunOnceAsync`, buscar compromisos `pending` con más de 7 días y avisar con `commitment_overdue` y clave de deduplicación por compromiso.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Modules/Devices/DeviceModule.cs` | Tope de 5 dispositivos (hecho) |
| `Pactogg.Api/Modules/Auth/AuthService.cs`, `AccountService.cs` | Quitar dispositivos al revocar todo (hecho) |
| `Pactogg.Api/Common/Middleware/IdempotencyMiddleware.cs` | Clave opcional (006-T8) |
| `Pactogg.Api/Modules/Contracts/ContractEndpoints.cs`, `Evidence/EvidenceEndpoints.cs` | Marcar CON-01 y EVI-02 (006-T8) |
| `Pactogg.Api/Modules/Contracts/Lifecycle/ContractLifecycleService.cs` | Recordatorio de compromisos (006-T9) |
| DTO de grupos, miembros, compromisos y perfil | `updated_at` (006-T10) |
| `IntegrationTest/Spec006PushTests.cs` | Pruebas 006-AC3, AC4 (hecho) |

## Dependencias

Cuenta de servicio de Firebase en SSM; `google-services.json` en la app; canal `pactogg_default`.

## Riesgos

Cola en memoria; push real sin probar en producción hasta tener la credencial.

## Orden

1. Formalizar y cerrar huecos (hecho). 2. 006-T8 y 006-T10 antes de que móvil conecte Room (fila 80). 3. 006-T9 con compromisos (sprint 4). 4. Parte móvil.
