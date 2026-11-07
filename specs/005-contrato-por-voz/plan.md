# Plan — 005 Contrato por voz con IA

[Spec](spec.md).

## Enfoque

Un servicio `ContractDraftService` dentro de `Modules/Contracts/Voice` que valida, limita, busca en caché, arma la instrucción, llama al adaptador y valida la salida. El proveedor queda detrás de `IContractDraftProvider` (patrón adaptador). Sin proveedor configurado se usa `UnavailableDraftProvider`, que siempre responde 503.

## Diseño

1. Validar membresía (`GroupAccess`) y longitud de la frase.
2. Rate limit con la política nueva `ai` en `RedisRateLimitMiddleware` (5/min) y un contador diario por usuario en Redis con respaldo local (20/día).
3. Normalizar la frase (minúsculas, espacios) y calcular `sha256(gid + frase)`; buscar en `ICacheService` (10 min).
4. Cargar apodos de miembros activos (`IGroupMemberRepository` + `IUserRepository`, proyección solo de apodo e id; el id no se envía al modelo).
5. Instrucción del sistema fija + bloque de datos delimitado (`<frase>…</frase>`, `<miembros>…</miembros>`).
6. Llamar al proveedor con timeout de 8 s; validar JSON con `System.Text.Json` y un validador propio (sin campos extra, tipos y longitudes); un reintento si no cumple.
7. Emparejar rival por apodo; aplicar reglas de CON-01 (`ContractRules`) a cada campo; armar `missing_fields`.
8. Guardar en caché y responder. Logs solo con `request_id`, duración, `cached` y resultado; nunca la frase.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Modules/Contracts/Voice/IContractDraftProvider.cs` | Adaptador |
| `Pactogg.Api/Modules/Contracts/Voice/ContractDraftService.cs` | Lógica de R1–R13, R15 |
| `Pactogg.Api/Modules/Contracts/Voice/DraftSchemaValidator.cs` | Validación de la salida |
| `Pactogg.Api/Modules/Contracts/Voice/Providers/*.cs` | Implementación del proveedor elegido (Q-007) |
| `Pactogg.Api/Modules/Contracts/ContractEndpoints.cs` | Ruta CON-08 |
| `Pactogg.Api/Modules/Contracts/ContractService.cs`, `Dtos/ContractDtos.cs`, `Domain/Contract.cs` | `source=voice` y `original_phrase` (R14) |
| `Pactogg.Api/Common/Middleware/RedisRateLimitMiddleware.cs`, `Infrastructure/Options/RateLimitOptions.cs` | Política `ai` |
| `Pactogg.Api/appsettings.json` | Sección `Ai` sin secretos |
| `deploy/terraform/parameters.tf`, `user_data.sh.tftpl` | `Ai__ApiKey` en SSM (se agrega junto con SMTP y Firebase) |
| `IntegrationTest/VoiceContractTests.cs`, `UnitTest/DraftSchemaValidatorTests.cs` | Pruebas |
| `api/openapi.yaml`, `docs/API-movil.md` | Contrato |

## Dependencias

Proveedor de IA (Q-007, lo entrega el equipo), Redis, moderación al lanzar con CON-01.

## Riesgos

Cuotas del proveedor; latencia; calidad en español. Mitigación: caché, límites, fallback manual.

## Orden

1. Adaptador + proveedor falso + validador (sin proveedor real). 2. Endpoint y reglas. 3. `source=voice` en CON-01. 4. Pruebas AC1–AC13. 5. Proveedor real cuando llegue el modelo. 6. Prueba con 20 frases.
