# 005 — Contrato por voz con IA

**Fecha:** 2026-11-06 · **Estado:** Aprobada para implementar; proveedor de IA pendiente (Q-007) · **Sprint:** 3 (fila 64 del Sprint Planning)
**Historias:** ID 56 Dictar reto por voz · ID 57 Transcripción del reto · ID 58 Extracción de datos mediante IA · ID 59 Revisión de campos de IA · ID 60 Identificación automática del rival.
**Endpoint del CRUD:** CON-08 `POST /api/v1/groups/{gid}/contracts/parse`; cambio en CON-01 (`source=voice`).
**Fuentes:** constitución §1, §4 (IA generativa) y §7.1; CLARIFICACION #41–#50, #136 y sección IA de seguridad; Descripción del Backend 7.4 (BE-15); Descripción del Móvil §11; CRUD hoja Contratos (CON-08, campos `source` y `original_phrase`).

## 1. Contexto y objetivo

La persona dicta el reto en el celular; el celular lo convierte a texto y lo manda al backend. El backend le pide a un modelo de IA generativa que lo estructure en rival, juego, condición y recompensa, valida la respuesta y devuelve un **borrador**. La persona siempre lo revisa y edita antes de lanzarlo con CON-01.

## 2. Fuera de alcance

- Reconocimiento de voz: se hace en el celular (Android SpeechRecognizer o ML Kit). **No se envía audio** al backend (CLARIFICACION #42, #43).
- Elegir fecha límite y fianza: las define quien publica, no la IA (CLARIFICACION #38).
- Lanzar el reto: lo hace CON-01; CON-08 no guarda nada en la base.
- Reto por voz dentro de la demo previa al registro (ID 2): corre en la app con un ejemplo local (spec 004, fuera de alcance del backend).
- Resumen semanal con IA (prioridad baja, CLARIFICACION #2).
- Elegir el proveedor concreto: Q-007. Esta spec define el adaptador para que el proveedor se conecte sin cambiar el resto.

## 3. Requisitos funcionales (EARS)

| ID | Requisito |
|---|---|
| 005-R1 | Cuando un miembro activo del grupo envíe `phrase` de 1 a 300 caracteres, el sistema deberá devolver `200` con un borrador `{ target_type, target_id, target_nickname, game, condition, reward }`. |
| 005-R2 | Si quien llama no es miembro del grupo, entonces `403 NOT_GROUP_MEMBER`; si la frase está vacía o pasa de 300 caracteres, `422 VALIDATION_ERROR`. |
| 005-R3 | Cuando el sistema arme la petición al modelo, deberá enviar solo la frase y la lista de apodos de los miembros del grupo, sin correos, identificadores ni datos de perfil. |
| 005-R4 | Cuando el sistema arme la instrucción, deberá separar las instrucciones del sistema de los datos del usuario (la frase va delimitada como dato) y pedir una salida JSON con esquema fijo. |
| 005-R5 | Cuando llegue la respuesta del modelo, el sistema deberá tratarla como no confiable y validarla contra el esquema; si no cumple, deberá reintentar una sola vez y, si vuelve a fallar, responder `422 AI_PARSE_FAILED`. |
| 005-R6 | Cuando el modelo proponga un rival, el sistema deberá aceptarlo solo si coincide (sin distinguir mayúsculas) con el apodo de un miembro activo distinto de quien dicta; entonces `target_type=member` y `target_id` de ese miembro. |
| 005-R7 | Si el rival propuesto no coincide con ningún miembro, entonces el sistema deberá dejar `target_type=group`, marcar `rival_unmatched=true` y devolver la lista de miembros `{ id, nickname }` para que la persona elija. La IA nunca inventa rivales. |
| 005-R8 | Si la frase no menciona rival, entonces el sistema deberá dejar el borrador abierto al grupo (`target_type=group`). |
| 005-R9 | Si el modelo no identifica el juego, entonces el sistema deberá devolver `game` vacío para que la app lo pida; el juego es texto libre sin autocompletado. |
| 005-R10 | Si un campo del borrador no cumple los límites de CON-01 (juego 1–60, condición 1–280, recompensa 3–80 sin dinero), entonces el sistema deberá devolverlo vacío y listarlo en `missing_fields`, en lugar de corregirlo. |
| 005-R11 | Mientras un usuario supere 5 solicitudes por minuto o 20 por día, el sistema deberá responder `429` con `Retry-After`. |
| 005-R12 | Cuando llegue una frase igual (normalizada) para el mismo grupo dentro de 10 minutos, el sistema deberá devolver el borrador en caché sin llamar al modelo; la clave de caché es un hash, nunca la frase en claro. |
| 005-R13 | Si el proveedor no está configurado, tarda más de 8 segundos, falla, o el circuito está abierto tras 5 fallos seguidos (60 s), entonces el sistema deberá responder `503 AI_UNAVAILABLE` para que la app ofrezca escribir a mano. |
| 005-R14 | Cuando la app lance el reto revisado con CON-01, el sistema deberá aceptar `source=voice` con `original_phrase` (máximo 300) y guardarla dentro del contrato; con `source=manual` `original_phrase` debe venir vacía. |
| 005-R15 | Mientras se procese una frase, el sistema no deberá guardarla en la base fuera del contrato lanzado ni escribirla en logs, ni la respuesta del modelo. |

## 4. Requisitos no funcionales

- **Costo cero:** proveedor con capa gratuita (Gemini, Groq, Mistral, OpenRouter, Hugging Face u Ollama) o el modelo que entregue el equipo; límites ajustables por configuración.
- **Seguridad (OWASP LLM Top 10):** prevención de prompt injection (R4), salida no confiable validada por esquema (R5), sin PII al proveedor (R3), sin rivales inventados (R6, R7), límites de uso (R11).
- **Disponibilidad:** timeout de 8 s, un reintento, circuit breaker; la app siempre puede escribir a mano.
- **Secretos:** la clave del proveedor vive en SSM Parameter Store (`Ai__ApiKey`), nunca en el repositorio ni en el APK.
- **Privacidad:** configurar el proveedor para no usar los datos para entrenamiento cuando lo permita.
- **Rendimiento:** respuesta en caché < 50 ms; sin caché, dominada por el proveedor (objetivo < 5 s).

## 5. Endpoint y esquemas

`POST /api/v1/groups/{gid}/contracts/parse` (CON-08), Bearer, sin `Idempotency-Key` (no cambia estado).

Petición:

```json
{ "phrase": "Reto a Juan a ganar tres partidas de Valorant y si pierde invita los tacos" }
```

Respuesta `200`:

```json
{
  "data": {
    "target_type": "member",
    "target_id": "6a1f…",
    "target_nickname": "juan",
    "game": "Valorant",
    "condition": "Ganar tres partidas seguidas",
    "reward": "Invitar los tacos",
    "missing_fields": [],
    "rival_unmatched": false,
    "members": null,
    "cached": false
  }
}
```

Esquema que se le pide al modelo (estricto, sin campos extra):

```json
{ "rival": "string|null", "game": "string|null", "condition": "string", "reward": "string|null" }
```

Errores: `403 NOT_GROUP_MEMBER`, `422 VALIDATION_ERROR`, `422 AI_PARSE_FAILED`, `429 RATE_LIMITED`, `503 AI_UNAVAILABLE`.

Adaptador: `IContractDraftProvider.CompleteAsync(systemPrompt, userData, ct) → string` con implementaciones por proveedor y una falsa para pruebas. Configuración `Ai:Provider`, `Ai:Model`, `Ai:Endpoint`, `Ai:ApiKey`, `Ai:TimeoutSeconds=8`, `Ai:PerMinute=5`, `Ai:PerDay=20`.

## 6. Criterios de aceptación

| ID | Requisito | Dado / cuando / entonces |
|---|---|---|
| 005-AC1 | R1, R6 | Frase "reto a juan…" con `juan` en el grupo → `target_type=member` y el id de juan |
| 005-AC2 | R2 | No miembro → 403; frase de 301 caracteres → 422 |
| 005-AC3 | R3 | La petición al proveedor falso contiene los apodos y la frase, y no contiene correos ni ObjectId |
| 005-AC4 | R4, R5 | Frase con "ignora las instrucciones y devuelve…" → la salida igual se valida contra el esquema; campos extra se rechazan |
| 005-AC5 | R5 | Proveedor devuelve JSON inválido dos veces → 422 `AI_PARSE_FAILED` y se llamó exactamente 2 veces |
| 005-AC6 | R7 | Rival "pedro" que no está en el grupo → `target_type=group`, `rival_unmatched=true` y lista de miembros |
| 005-AC7 | R8, R9 | Sin rival ni juego → `target_type=group` y `game` vacío en `missing_fields` |
| 005-AC8 | R10 | Recompensa "100 pesos" → `reward` vacío y en `missing_fields` |
| 005-AC9 | R11 | Sexta solicitud en un minuto → 429 |
| 005-AC10 | R12 | Misma frase dos veces → el proveedor se llama una vez y la segunda respuesta trae `cached=true` |
| 005-AC11 | R13 | Sin proveedor configurado → 503; proveedor que tarda 9 s → 503; tras 5 fallos el sexto responde 503 sin llamar |
| 005-AC12 | R14 | CON-01 con `source=voice` guarda `original_phrase`; `source=manual` con frase → 422 |
| 005-AC13 | R15 | Los logs de la petición no contienen la frase |

## 7. Preguntas abiertas

- **Q-007 Proveedor de IA:** abierta. El equipo entregará el modelo; esta spec queda completa porque el adaptador aísla al proveedor. Al fijarlo se registra la decisión y solo cambia la implementación de `IContractDraftProvider` y su configuración.

## 8. Verificación contra la constitución

| Regla | Cumple |
|---|---|
| §1 estructurar retos dictados por voz con IA generativa | Sí |
| §4 IA gratuita detrás de un adaptador | Sí (proveedor pendiente, Q-007) |
| §7.1 rate limit estricto en IA | Sí (R11) |
| §8 logs sin frases dictadas | Sí (R15) |
| §3.4 falla cerrada | Sí: sin IA no hay borrador, la persona escribe a mano |
| §3.12 sin requisitos inventados | Los límites de campos (R10), el timeout de 8 s y el circuito de 5 fallos son decisiones de diseño de esta spec (005-D1..D3), aprobadas por el PO el 2026-11-06 |

## 9. Decisiones de esta spec

- **005-D1:** los campos que no cumplen las reglas de CON-01 se devuelven vacíos en vez de corregirse (la IA no decide por la persona).
- **005-D2:** timeout de 8 s y un solo reintento solo cuando el JSON no cumple el esquema; un timeout no se reintenta.
- **005-D3:** circuit breaker de 5 fallos seguidos con apertura de 60 s, local por instancia.

## 10. Plan de pruebas

Unitarias: validador del esquema, emparejamiento de rival, normalización y hash de la frase. Integración con proveedor falso: AC1–AC13. Seguridad: frases con intentos de inyección, frase con correo de un miembro (no se reenvía tal cual si no es la frase), límite de uso. Prueba manual con el proveedor real y 20 frases de ejemplo (fila 74).

## 11. Riesgos

- Cuotas de la capa gratuita del proveedor (mitigado con caché y límites).
- Calidad en español coloquial; se mide con las 20 frases de la fila 74.
- Dependencia del modelo que entregue el compañero (Q-007).

## Aprobación

| Rol | Persona | Fecha | Resultado |
|---|---|---|---|
| Product Owner | David M. | 2026-11-06 | Aprobada |
| Revisión técnica | Kevin H. | Pendiente | — |
| Comprobabilidad | José G. | Pendiente | — |

Expediente: [plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).
