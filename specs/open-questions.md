# Preguntas abiertas — PactoGG Backend

**Última actualización:** 2026-11-04  
**Responsable de resolver:** David (Product Owner)  
**Regla:** el agente no implementa una decisión nueva sin respuesta registrada aquí.

## Resumen

| ID | Tema | Impacto | Estado |
|---|---|---|---|
| Q-001 | Ruta de contratos: plana vs anidada | Alto | Resuelta D-14 |
| Q-002 | Apodo obligatorio en registro vs apodo temporal | Alto | Resuelta D-15 |
| Q-003 | Política de contraseña | Medio | Resuelta D-16 |
| Q-004 | Evidencia por enlace: moderación y S3 | Alto | Resuelta D-18 |
| Q-005 | Lista final de dominios permitidos | Medio | Resuelta D-18 |
| Q-006 | Redis externo gratuito vs contenedor | Medio | Resuelta F2-D19 |
| Q-007 | Proveedor gratuito de IA generativa | Alto | Abierta |
| Q-008 | Código HTTP de logout | Bajo | Resuelta D-19 |
| Q-009 | .NET 8 vs .NET 10 | Alto | Resuelta |
| Q-010 | Ruta de torneos: plana vs anidada | Alto | Resuelta D-20 |
| Q-011 | Alias de rutas y resolución de `group_id` | Alto | Resuelta D-14/D-20 |
| Q-012 | Moderación de enlaces y oEmbed | Medio | Abierta |
| Q-013 | Secuencia de subida de foto y URL firmada | Alto | Resuelta D-17 |
| Q-014 | Alcance de `GET /tournaments` | Medio | Resuelta D-20 |
| Q-015 | Versión del aviso de privacidad en registro | Bajo | Resuelta D-21 |
| Q-016 | Opciones de fianza por grupo | Bajo | Resuelta D-22 |
| Q-017 | Transición a `voting` por evidencia | Alto | Resuelta F2-D20 |
| Q-018 | Objetivo del contrato y auto-reto | Medio | Resuelta F2-D21 |
| Q-019 | Longitudes de apodo y nombre de grupo | Medio | Resuelta F2-D50 |
| Q-020 | Timing de login para correo inexistente | Medio | Resuelta F2-D51 |
| Q-021 | Moderación del nombre de grupo | Medio | Resuelta F2-D52 |
| Q-022 | Cancelación de un reto aceptado por el administrador | Medio | Resuelta 2026-11-11 |

---

## Q-001 — Ruta de contratos

**Pregunta:** ¿La creación de contratos se publica en `POST /api/v1/contracts` o en `POST /api/v1/groups/{gid}/contracts`?

**Fuentes:**

- Directriz de evaluación: `POST /api/v1/contracts`.
- `CRUD_y_Endpoints_PactoGG.xlsx`, CON-01: `POST /groups/{gid}/contracts`.
- `AGENTS.md` del proyecto: mantener ambas formas.

**Impacto:** alto; define controladores, autorización por grupo y contrato OpenAPI.

**Propuesta:** implementar la ruta canónica `POST /api/v1/groups/{gid}/contracts` y un alias `POST /api/v1/contracts` que reciba `group_id` en el cuerpo o lo resuelva por pertenencia. Ambas comparten el mismo servicio de aplicación.

**Decide:** David (PO). **Estado:** resuelta (2026-10-30, D-14): ruta plana `POST /api/v1/contracts` con `group_id` en el cuerpo y alias anidado; pertenencia obligatoria.

---

## Q-002 — Apodo en el registro

**Pregunta:** ¿El registro exige apodo definitivo o crea un apodo temporal?

**Fuentes:**

- Directriz de evaluación: “Registro de usuario con apodo obligatorio”.
- Clarificaciones, preguntas 6 y 7: el registro solo pide correo, contraseña y consentimiento; crea un apodo temporal único; el definitivo se define después de la verificación.
- `CRUD_y_Endpoints_PactoGG.xlsx`, USR-01: el backend crea un apodo temporal único.

**Impacto:** alto; afecta validación, unicidad global y flujo móvil.

**Propuesta:** aceptar `nickname` opcional en el registro. Si viene, validarlo y usarlo; si no viene, generar `JugadorNNNN` temporal. La app obliga a cambiarlo antes de crear o unirse a grupos. Si la evaluación exige el campo, documentarlo como obligatorio en la especificación de Auth con el valor temporal generado por el cliente.

**Decide:** David (PO). **Estado:** resuelta (2026-10-30, D-15): `nickname` obligatorio en el registro.

---

## Q-003 — Política de contraseña

**Pregunta:** ¿Se aplica 8+ caracteres con letra y número o 12–128 sin reglas de composición?

**Fuentes:**

- `CRUD_y_Endpoints_PactoGG.xlsx`, USR-01: contraseña de 8+ caracteres con letra y número.
- Clarificaciones, seguridad: mínimo 12, máximo 128, sin reglas de composición, frases largas, verificación contra contraseñas filtradas.

**Impacto:** medio; validadores, pruebas y experiencia móvil.

**Propuesta:** aplicar la decisión del Product Owner (12–128, sin composición, Argon2id) y actualizar el CRUD. La regla de 8+ con composición queda obsoleta.

**Decide:** David (PO). **Estado:** resuelta (2026-10-30, D-16): 12–128 caracteres, sin composición, Argon2id.

---

## Q-004 — Evidencia por enlace y S3

**Pregunta:** ¿Los enlaces de evidencia se moderan y se almacenan en S3, o solo se valida el dominio?

**Fuentes:**

- Directriz de evaluación: “Envío de enlace externo de video o foto con moderación previa obligatoria antes de almacenar en AWS S3”.
- Backend, sección 7.5: el enlace solo se acepta si el dominio está permitido; se guarda proveedor, URL y metadatos; no se suben videos.
- Clarificaciones, preguntas 55–58 y D-12: HTTPS obligatorio, dominio permitido, no se descarga el enlace, no se almacena video.
- `CRUD_y_Endpoints_PactoGG.xlsx`, EVI-02: `object_key` o `url` válido.

**Impacto:** alto; define el flujo de evidencia y el alcance de moderación.

**Propuesta:** las fotos se moderan con Rekognition antes de S3. Los enlaces no se suben a S3 ni se descargan; se valida HTTPS, dominio permitido, formato y se guardan metadatos. La “moderación previa” de un enlace es validación de dominio y, si aplica, metadatos de oEmbed; no análisis del contenido remoto.

**Decide:** David (PO / docente). **Estado:** resuelta (2026-10-30, D-18): enlaces solo validación de dominio; fotos con moderación antes de S3.

---

## Q-005 — Dominios permitidos

**Pregunta:** ¿Cuál es la lista final y se permiten subdominios?

**Fuentes:**

- Backend, sección 3 y clarificación 56: `youtube.com`, `youtu.be`, `twitch.tv`, `clips.twitch.tv`, `medal.tv`, `streamable.com`.
- Clarificación 57: se permiten el dominio exacto, `www` y `m`; otros subdominios no.

**Impacto:** medio; validación de enlaces y pruebas.

**Propuesta:** lista en configuración (`Evidence:AllowedHosts`), comparación exacta de host normalizado, sin subdominios arbitrarios. Incluir `www.` y `m.` solo para los dominios que los soporten.

**Decide:** David (PO). **Estado:** resuelta (2026-10-30, D-18): allowlist configurable, host exacto más `www.` y `m.`, sin descarga.

---

## Q-006 — Redis

**Pregunta:** ¿Redis será un servicio externo gratuito (por ejemplo Upstash) o un contenedor?

**Fuentes:**

- Backend, sección 16: decidir si Redis será servicio externo gratuito o contenedor.
- Clarificaciones: Redis gratuito, por ejemplo Upstash o contenedor.

**Impacto:** medio; despliegue, TLS, latencia y costo.

**Propuesta:** contenedor Redis en la misma VPC durante desarrollo y evaluación para evitar latencia y límites de un servicio externo; documentar la alternativa Upstash si se requiere fuera de AWS.

**Decide:** Kevin/David. **Estado:** resuelta (2026-11-04, F2-D19): Redis en contenedor (`redis:7-alpine`, con contraseña en SSM) dentro de la instancia 1 y compartido por ambas instancias; ElastiCache se retira por costo (requisito "costo cero").

---

## Q-007 — Proveedor de IA generativa

**Pregunta:** ¿Qué proveedor gratuito se usará para estructurar el contrato por voz?

**Fuentes:**

- Clarificación 41: Gemini, Groq, Mistral, OpenRouter, Hugging Face u Ollama; recomendación Gemini o Groq.
- Backend, sección 7.4: adaptador, timeout, reintento, caché y fallback manual.

**Impacto:** alto; contrato por voz, límites diarios y pruebas.

**Propuesta:** Gemini API o Groq detrás de `IContractParser`; límite de 20 solicitudes/día y 5/minuto; timeout y circuit breaker; salida validada por esquema; fallback a escritura manual.

**Decide:** David (PO). **Estado:** abierta. Desde v0.12.0 el adaptador (`IContractDraftProvider`) y uno genérico OpenAI-compatible están listos (005-D4, F2-D73); al fijar el proveedor solo se configura.

---

## Q-008 — Código HTTP de logout

**Pregunta:** ¿Logout responde `204 No Content` o `200 OK`?

**Fuentes:**

- `CRUD_y_Endpoints_PactoGG.xlsx`, AUTH-04: 204, 401.
- Directriz de evaluación: no especifica código.

**Impacto:** bajo; contrato OpenAPI y pruebas.

**Propuesta:** `204 No Content` por consistencia con el CRUD y ausencia de cuerpo.

**Decide:** David (PO). **Estado:** resuelta (2026-10-30, D-19): `204 No Content`.

---

## Q-009 — .NET 8 vs .NET 10

**Pregunta:** ¿Se usa .NET 8 LTS o .NET 10 LTS?

**Fuentes:**

- Backend D-06: .NET 10 LTS; si el entorno del curso exige .NET 8, se usa .NET 8 LTS.
- Directriz de esta fase: .NET 8.

**Impacto:** alto; SDK, paquetes y pipeline.

**Resolución:** **.NET 8 LTS** por directriz explícita del proyecto en esta fase.

**Estado:** resuelta (F1-D01). Cualquier cambio requiere enmienda a `constitution.md`.

---

## Q-010 — Ruta de torneos

**Pregunta:** ¿La consulta de torneos se publica en `GET /api/v1/tournaments` o en `GET /api/v1/groups/{gid}/tournaments`?

**Fuentes:**

- Directriz de evaluación: `GET /api/v1/tournaments`.
- `CRUD_y_Endpoints_PactoGG.xlsx`, TOR-02: `GET /groups/{gid}/tournaments`; TOR-03: `GET /tournaments/{id}`.

**Impacto:** alto; controladores, filtros y contrato.

**Propuesta:** `GET /api/v1/tournaments` devuelve los torneos de los grupos a los que pertenece el usuario, con filtros `status`, `game`, `mine`, `limit` y `cursor`. Mantener `GET /api/v1/groups/{gid}/tournaments` como forma canónica anidada.

**Decide:** David (PO). **Estado:** resuelta (2026-10-30, D-20): `GET /tournaments` por grupos del usuario; alias anidado se conserva.

---

## Q-011 — Alias de rutas y resolución de grupo

**Pregunta:** ¿Cómo se resuelve `group_id` en las rutas planas de evaluación?

**Fuentes:**

- Directriz de evaluación y `AGENTS.md`.
- Backend, sección 8: toda consulta filtra por `group_id` y pertenencia.

**Impacto:** alto; anti-IDOR y autorización.

**Propuesta:** las rutas planas reciben `group_id` en el cuerpo (crear) o lo derivan del recurso y verifican pertenencia (leer/actualizar). Nunca se confía en un `group_id` del cliente sin comprobar que el usuario es miembro activo. Las rutas anidadas siguen siendo canónicas.

**Decide:** David/Kevin. **Estado:** resuelta (2026-10-30, D-14/D-20): ruta plana con `group_id` en el cuerpo y verificación de pertenencia.

---

## Q-012 — Moderación de enlaces y oEmbed

**Pregunta:** ¿Se consulta oEmbed para título/miniatura y cómo se protege de SSRF?

**Fuentes:**

- Backend, sección 7.5: si el proveedor lo ofrece, título y miniatura con su endpoint público de incrustación.
- Clarificaciones, seguridad: validar dominio, bloquear IP privadas, limitar redirecciones y usar timeout.

**Impacto:** medio; dependencia externa y superficie SSRF.

**Propuesta:** oEmbed opcional y apagado por defecto. Si se activa: allowlist de hosts, HTTPS, resolución DNS sin IP privadas/loopback/link-local, máximo 3 redirecciones, timeout de 3 s, tamaño máximo de respuesta y sin guardar el cuerpo remoto.

**Decide:** Kevin/David. **Estado:** abierta.

---

## Q-013 — Secuencia de foto y URL firmada

**Pregunta:** ¿El móvil envía la imagen al backend para moderarla o sube directo a S3 con URL firmada?

**Fuentes:**

- Backend, sección 7.5: el móvil envía la imagen al backend para analizarla en memoria; si aprueba, el backend entrega URL firmada; el móvil sube directo a S3 y el backend registra la evidencia.
- `CRUD_y_Endpoints_PactoGG.xlsx`, EVI-01: `POST /contracts/{id}/evidence/upload-url` entrega URL firmada “para subir el archivo aprobado por moderación”.
- EVI-02: registra la evidencia tras la subida aprobada o un enlace.

**Impacto:** alto; flujo móvil, tamaño de payload y moderación.

**Propuesta:** flujo en dos pasos:
1. `POST /contracts/{id}/evidence/upload-url` recibe metadata y la imagen (o un identificador temporal), valida tipo/tamaño/magic bytes, modera en memoria y, si aprueba, devuelve URL firmada.
2. El móvil sube a S3.
3. `POST /contracts/{id}/evidence` confirma `object_key` y registra la evidencia aprobada.

Si el equipo prefiere una sola llamada, `POST /contracts/{id}/evidence` acepta `multipart/form-data` y hace todo el flujo. Requiere decisión para el contrato OpenAPI.

**Decide:** David/Kevin. **Estado:** resuelta (2026-10-30, D-17): una sola llamada `multipart/form-data`; moderar en memoria y subir a S3 tras aprobar.

---

## Q-014 — Alcance de `GET /tournaments`

**Pregunta:** ¿La lista es global, por grupo o “mis torneos”?

**Fuentes:**

- Directriz de evaluación: “Consulta de torneos exprés y brackets”.
- CRUD TOR-02/TOR-03: por grupo y por id.

**Impacto:** medio; filtros, paginación y caché.

**Propuesta:** por defecto, torneos de los grupos del usuario (`mine=true` implícito); filtros `status`, `game`, `group_id` (solo si es miembro), `limit` y `cursor`. El detalle y bracket siguen en `GET /tournaments/{id}` y `GET /tournaments/{id}/bracket`.

**Decide:** David (PO). **Estado:** resuelta (2026-10-30, D-20): por defecto torneos de los grupos del usuario; filtro `group_id` solo si es miembro.

---

## Q-015 — Versión del aviso de privacidad

**Pregunta:** ¿Qué identificador de versión se acepta en el registro y dónde vive la versión vigente?

**Fuentes:**

- Backend, sección 7.1: registra la versión del aviso aceptada y la fecha.
- Clarificaciones: aviso versionado y consentimiento.

**Impacto:** bajo; validación y trazabilidad.

**Propuesta:** `consent.version` como texto semver (por ejemplo `2026-10-01`), validado contra la versión vigente configurada; guardar `version`, `accepted_at` y `ip_hash` opcional. No bloquear el registro por una versión antigua si el PO autoriza aceptar la vigente.

**Decide:** David. **Estado:** resuelta (2026-10-30, D-21): versión vigente configurable y consentimiento registrado.

---

## Q-016 — Opciones de fianza por grupo

**Pregunta:** ¿Se mantienen 25/50/100 o se permiten hasta 5 opciones entre 10 y 200?

**Fuentes:**

- Backend, tabla de reglas: 25, 50, 100 (configurable por grupo).
- Clarificación 86: el administrador puede definir hasta 5 opciones entre 10 y 200.

**Impacto:** bajo; validación de configuración.

**Propuesta:** valores por defecto 25/50/100; el administrador puede definir hasta 5 opciones en el rango 10–200. La fianza de cada contrato debe pertenecer a las opciones del grupo.

**Decide:** David. **Estado:** resuelta (2026-10-30, D-22): 25/50/100 por defecto; hasta 5 opciones entre 10 y 200.

---

## Q-017 — Transición a `voting` por evidencia

**Pregunta:** ¿El contrato pasa a `voting` con la primera evidencia aprobada o al alcanzar el máximo de tres evidencias?

**Fuentes:**

- Spec histórica `R-EVI-06`: tras la primera evidencia aprobada el contrato pasa a `voting`.
- Implementación actual en `Pactogg.Api/Modules/Evidence/EvidenceService.cs`: la transición ocurre al llegar a 3 evidencias.
- `specs/EVI-02-evidence/spec.md`, decisión `EVI02-D3`.
- Prueba `Evidence_RejectsInvalidImageAndCapsAtThreeLinks`.

**Impacto:** alto; define la máquina de estados, el inicio de votación y las pruebas de aceptación.

**Propuesta:** ratificar el comportamiento implementado (transición al llegar a 3) o corregirlo a la primera evidencia; en cualquier caso, actualizar OpenAPI, pruebas y `EVI-02` antes de cambiar código.

**Decide:** David (PO). **Estado:** resuelta (2026-11-04, F2-D20) con CLARIFICACION #30, #59 y #60: la primera evidencia aprobada pasa el contrato a `voting`; se admiten hasta 3 evidencias mientras no haya votos; la cuarta devuelve `409 EVIDENCE_LIMIT_REACHED`.

---

## Q-018 — Objetivo del contrato y auto-reto

**Pregunta:** ¿`target_type` usa `user|group` o `group|member`, y debe impedirse que un usuario se rete a sí mismo?

**Fuentes:**

- Spec histórica `R-CON-04`: `target_type` `user` o `group`; si es `user`, el objetivo debe ser un miembro distinto del retador.
- Implementación actual: `target_type` `group|member` y no se bloquea el auto-reto.
- `specs/CON-01-create-contract/spec.md`, decisiones `CON01-D2` y `CON01-D3`.

**Impacto:** medio; validación de contratos, compatibilidad del cliente Android y pruebas antiabuso.

**Propuesta:** ratificar `group|member` y decidir si el auto-reto se prohíbe. Si se prohíbe, agregar validación y prueba antes de cambiar el contrato.

**Decide:** David (PO). **Estado:** resuelta (2026-11-04, F2-D21) con CLARIFICACION #32 y #73: `target_type` es `group|member` (persona o todo el grupo); retarse a sí mismo devuelve `422 INVALID_TARGET`.

---

## Q-019 — Longitudes de apodo y nombre de grupo

**Pregunta:** ¿Se mantienen los límites implementados o los del texto histórico?

**Fuentes:**

- Texto histórico: apodo 3–30 y nombre de grupo 3–30.
- Implementación actual: apodo 3–24 y nombre de grupo 2–40.
- `specs/USR-01-register/spec.md` (`USR01-D4`) y `specs/GRP-01-create-group/spec.md` (`GRP01-D3`).

**Impacto:** medio; validadores, formularios móviles, OpenAPI y pruebas de borde.

**Propuesta:** elegir una única fuente y alinear DTOs, OpenAPI, specs y pruebas. No cambiar límites sin decisión del PO.

**Decide:** David (PO). **Estado:** resuelta (2026-11-06, F2-D50): se quedan los límites implementados, apodo 3–24 y nombre de grupo 2–40; se corrigen los documentos, no el código.

---

## Q-020 — Timing de login para correo inexistente

**Pregunta:** ¿Se iguala el trabajo de hash cuando el correo no existe para evitar enumeración por tiempo?

**Fuentes:**

- Spec histórica `R-AUTH-13`: misma respuesta y mismo trabajo de hash para correo inexistente.
- Implementación actual: responde genérico, pero no ejecuta una verificación ficticia de Argon2id cuando el usuario no existe.
- `specs/AUTH-01-login/spec.md`, decisión `AUTH01-D3`.

**Impacto:** medio; superficie de enumeración de cuentas y pruebas de seguridad.

**Propuesta:** ejecutar una verificación Argon2id ficticia con un hash constante cuando no haya usuario y agregar una prueba de tiempo o de trabajo de hash. Requiere decisión antes de cambiar el flujo.

**Decide:** David (PO). **Estado:** resuelta (2026-11-06, F2-D51): con un correo inexistente (o una cuenta solo de Google) el login verifica un hash Argon2id ficticio para igualar el trabajo. Prueba `SddDecisionTests.Q020_LoginWithUnknownEmail_DoesTheSameHashWorkAsAWrongPassword`.

---

## Q-021 — Moderación del nombre de grupo

**Pregunta:** ¿El nombre del grupo debe pasar moderación de texto antes de almacenarse?

**Fuentes:**

- Spec histórica `R-GRP-03`: el nombre debe pasar moderación de texto.
- Implementación actual: `GroupService` valida longitud, pero no invoca moderación.
- `specs/GRP-01-create-group/spec.md`, decisión `GRP01-D4`.

**Impacto:** medio; política de contenido, fallo cerrado y pruebas de abuso.

**Propuesta:** invocar el mismo adaptador de moderación de texto usado en contratos, con fallo cerrado y `422 CONTENT_REJECTED`; o retirar formalmente el requisito. No cambiar el comportamiento sin decisión.

**Decide:** David (PO). **Estado:** resuelta (2026-11-06, F2-D52): el nombre del grupo pasa por la moderación de texto antes de guardarse; rechazo `422 CONTENT_REJECTED`, moderador caído `503` (falla cerrada). Prueba `SddDecisionTests.Q021_GroupNameRejectedByModeration_Returns422AndIsNotStored`.

---

## Q-022 — Cancelación de un reto aceptado por el administrador

**Pregunta:** ¿Con qué endpoint cancela el administrador un reto ya aceptado (`in_progress`) y qué pasa con la fianza?

**Fuentes:**

- CLARIFICACION #35: después de `open` solo se cancela por salida o expulsión de un participante, o por decisión del administrador.
- CRUD hoja Contratos: CON-05 solo cancela retos que nadie ha aceptado; no hay endpoint para el administrador.
- VOT-04 resuelve votaciones (empate, vencida, sin jurados), no retos en `in_progress`.

**Impacto:** medio; spec 004 lo deja fuera de alcance.

**Propuesta:** `POST /contracts/{id}/cancel` solo para `admin`, desde `in_progress`, con motivo obligatorio, devolución de la fianza (`bond_release`) y auditoría; requiere agregar el ID al CRUD.

**Decide:** David (PO). **Estado:** resuelta (2026-11-11): el PO aprobó la propuesta ("que tal si alguien hace un reto imposible"). CON-09 `POST /contracts/{id}/cancel` para administradores, desde `open` o `in_progress`, con motivo y devolución de la fianza; un administrador que aceptó el reto no puede cancelarlo él mismo. Spec 004-R16.

---

## Plantilla para nuevas preguntas

```markdown
## Q-XXX — Título

**Pregunta:**
**Fuentes:**
**Impacto:**
**Propuesta:**
**Decide:**
**Estado:** abierta | resuelta (fecha, decisión)
```
