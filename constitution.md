# Constitución del Backend — PactoGG

**Versión:** 1.0.0  
**Estado:** Ratificada para la Fase 1  
**Fecha:** 2026-10-30  
**Equipo:** PueblOS · Grupo 10IDGS · UTTT  
**Product Owner:** David  
**Revisión técnica:** Kevin  
**Verificación de comprobabilidad:** José  
**Documentación:** Diego

Esta constitución es la norma superior del backend de PactoGG. Toda especificación, plan, tarea, código, prueba, pipeline e infraestructura DEBE cumplirla. Si una decisión la contradice, prevalece este documento hasta que se apruebe una enmienda formal.

---

## 1. Propósito y alcance

PactoGG es una red social privada para grupos de amigos que convierte retos, promesas y torneos de videojuegos en contratos estructurados con historial inmutable. El backend:

- Autentica usuarios con correo/contraseña y Google OAuth 2.0.
- Administra grupos privados, miembros, roles y códigos de invitación.
- Guarda contratos, aplica su máquina de estados, fianzas y recompensas.
- Modera evidencias antes de almacenarlas.
- Coordina jurados, votaciones y resolución de contratos.
- Lleva el libro inmutable de puntos y los compromisos.
- Crea torneos entre grupos, brackets y reparto de bolsas.
- Estructura retos dictados por voz mediante IA generativa.
- Envía notificaciones push y registra observabilidad y auditoría.

**REGLA DE ORO:** los puntos son virtuales y sin valor monetario. El backend NO DEBE procesar pagos, compras, ventas ni canjes de puntos, ni exponer símbolos o endpoints de dinero real.

---

## 2. Fuentes de verdad y jerarquía

Orden de precedencia ante contradicciones:

1. `PactoGG — Respuestas de clarificación revisadas (.NET y Kotlin).txt` (decisiones del Product Owner).
2. `PactoGG — Descripción del Backend.docx` (fuente de verdad del backend).
3. `CRUD_y_Endpoints_PactoGG.xlsx` y `Componentes_Backend_y_Movil_PactoGG.xlsx`.
4. `Stack_Tecnologico_PactoGG.xlsx`.
5. `Requerimientos para el desarrollo del proyecto Móvil v3.pdf` (requisitos del docente).
6. Este documento y las especificaciones por módulo.

Toda contradicción DEBE registrarse en `specs/open-questions.md`; el agente NO DEBE resolverla por cuenta propia. Las decisiones del Product Owner mandan sobre los documentos fuente y obligan a actualizarlos.

---

## 3. Principios no negociables

1. **Spec-Driven Development:** primero especificación, luego plan, luego tareas y al final código. Si cambia el comportamiento, primero cambia la especificación.
2. **Seguridad por diseño:** OWASP ASVS nivel 2 y OWASP API Security Top 10 son requisitos de aceptación, no una fase final.
3. **Mínimo privilegio:** IAM, usuario de MongoDB, roles de grupo, roles por reto/torneo y scopes de tokens.
4. **Falla cerrada:** si la moderación, la autorización o la validación no pueden completarse, la operación se rechaza; nunca se degrada la seguridad.
5. **Aislamiento por grupo:** toda consulta DEBE filtrar por `group_id` y verificar pertenencia y propiedad del recurso. Sin excepciones.
6. **Historial inmutable:** movimientos de puntos, auditoría y eventos sensibles son solo de inserción; las correcciones se hacen con movimientos compensatorios.
7. **Sin secretos en el repositorio:** claves, cadenas de conexión, tokens y certificados viven en AWS Secrets Manager o Parameter Store. `.env` y `appsettings.*.local.json` están prohibidos en Git.
8. **Sin video almacenado:** las evidencias son fotos o enlaces. Un video solo se comparte como enlace externo.
9. **Moderación antes de almacenar:** ninguna foto llega a S3 sin aprobación de Rekognition; el texto libre pasa por Comprehend.
10. **Trazabilidad:** cada cambio cita IDs de componente (`BE-xx`) o endpoint (`AUTH-xx`, `GRP-xx`, `CON-xx`, `EVI-xx`, `TOR-xx`, etc.).
11. **Costo cero:** MongoDB Atlas M0, capa gratuita o créditos de AWS, Redis gratuito, Firebase, GitHub Actions y proveedor de IA gratuito. Toda decisión DEBE respetar los límites de M0.
12. **Sin invención de requisitos:** lo no definido se anota en `specs/open-questions.md`.

---

## 4. Stack tecnológico fijado

| Capa | Decisión | Versión / nota |
|---|---|---|
| Lenguaje / framework | C# + ASP.NET Core Web API | **.NET 8 LTS** por directriz explícita de esta fase (D-06: fallback permitido) |
| Arquitectura | Monolito modular REST | Un servicio, módulos por carpeta, prefijo `/api/v1` |
| Base de datos | MongoDB Atlas M0 | Driver oficial `MongoDB.Driver`, `IMongoDatabase` |
| Caché / tokens revocados | Redis | StackExchange.Redis / `IDistributedCache` |
| Autenticación | JWT Bearer + Google OAuth 2.0 | Access token 15 min; refresh rotativo |
| Hash de contraseña | Argon2id (fallback BCrypt si se justifica) | Mínimo 12, máximo 128 caracteres |
| Nube | AWS (costo cero con créditos) | 2 EC2 `t3.micro`, Caddy como balanceador + TLS Let's Encrypt, Redis en contenedor, S3 + CloudFront, Rekognition, Comprehend, CloudWatch, SSM Parameter Store. Sin ALB, NAT ni ElastiCache (F2-D19) |
| Contenedores | Docker | Imagen única del backend |
| IaC | Terraform | Crear y destruir; estado en S3 con bloqueo |
| CI/CD | GitHub Actions | SAST, SCA, secreto, contenedor, IaC, DAST, compuerta |
| Pruebas | xUnit + FluentAssertions + Moq + Testcontainers + Pact + NBomber + Stryker.NET | Ver sección 9 |
| Observabilidad | CloudWatch + logs JSON + `/health` | `request_id` en toda traza |
| Push | Firebase Cloud Messaging | Hasta 5 dispositivos por cuenta |
| IA generativa | Adaptador sobre proveedor gratuito | Gemini/Groq/Mistral/OpenRouter/HF/Ollama; pendiente de fijar |
| Documentación API | OpenAPI 3.1 | `api/openapi.yaml` es contrato vinculante |

**NO se usa:** Supabase, PostgreSQL, SQL Server, DynamoDB, DocumentDB, Azure, Google Cloud, microservicios, Kubernetes, gRPC, GraphQL, WebSocket, MQTT, AMQP, SOAP, API Gateway, BFF, CQRS ni colas como dependencia obligatoria.

---

## 5. Arquitectura y estructura

### 5.1 Estilo

Monolito modular desplegable en dos instancias EC2 detrás de Caddy, que actúa como balanceador (round-robin con health checks) y termina HTTPS con Let's Encrypt. Sin estado en memoria compartida: sesiones revocadas, caché y rate limiting viven en Redis, compartido por ambas instancias. Las instancias de la API son intercambiables. Se eligió así por el requisito del docente "configurado con costo cero" (F2-D19).

### 5.2 Solución .NET (8 proyectos)

```
PactoGG/
├── Pactogg.sln
├── Pactogg.Api/                  # ASP.NET Core Web API (proyecto principal)
├── ContractTest/                 # Contratos de API contra OpenAPI / Pact
├── DatabaseTest/                 # MongoDB real con Testcontainers
├── IntegrationTest/              # End-to-end con WebApplicationFactory
├── MutationTest/                 # Stryker.NET (umbral objetivo 60 %)
├── PerformanceTest/              # NBomber, carga ligera
├── SecurityTest/                 # Autorización, IDOR, JWT, rate limit, SSRF, NoSQL
└── UnitTest/                     # Reglas, máquina de estados, consenso, brackets
```

### 5.3 Módulos internos de `Pactogg.Api`

```
Modules/
  Auth/          # BE-11, BE-12, BE-27, BE-32
  Groups/        # BE-13
  Contracts/     # BE-14, BE-15, BE-18, BE-19, BE-20
  Evidence/      # BE-16, BE-17, BE-47, BE-48
  Tournaments/   # BE-21, BE-22, BE-23
Infrastructure/
  Mongo/ Redis/ Security/ Storage/ Moderation/
Common/          # middleware, DTOs base, errores, request_id
```

Cada módulo DEBE contener, cuando aplique: controladores, DTOs, validadores, servicios de aplicación, dominio, repositorios e interfaces de infraestructura. La lógica de negocio NO DEBE depender de MongoDB, Redis ni AWS directamente.

### 5.4 Orden de middleware

`request-id` → logs → compresión → seguridad y CORS → rate limit → autenticación → autorización → validación → manejo de errores.

---

## 6. Reglas de dominio obligatorias

### 6.1 Autenticación y cuentas

- Registro con correo y contraseña; verificación de correo obligatoria para crear o unirse a grupos.
- Apodo único global, sin distinguir mayúsculas; el registro crea un apodo temporal y la app obliga a cambiarlo antes de grupos (ver Q-002).
- Login devuelve access token JWT de 15 minutos y refresh token rotativo.
- Refresh reutilizado se considera robado: se revocan todas las sesiones del usuario.
- Logout revoca el refresh actual; `all=true` revoca todas las sesiones. Los tokens revocados viven en Redis hasta expirar.
- Contraseñas: Argon2id, mínimo 12 y máximo 128 caracteres, sin reglas de composición, con verificación contra listas de contraseñas filtradas.
- Bloqueo temporal con espera creciente tras 5 intentos fallidos; respuestas genéricas para evitar enumeración.
- Recuperación: token de un solo uso, hasheado, caducidad de 1 hora, misma respuesta exista o no la cuenta.
- Eliminación de cuenta: revoca sesiones y dispositivos, borra datos personales, fotos, tokens y consentimiento; anonimiza historial con el apodo “Usuario eliminado”.

### 6.2 Grupos y miembros

- Código de invitación de 6 caracteres; regenerable y desactivable por administradores.
- Máximo 20 miembros por límites de M0; mínimo 3 para lanzar retos.
- Hasta 5 grupos por usuario.
- Roles de grupo: `admin` y `member`. El creador no se puede expulsar ni degradar.
- Saldo inicial: 100 puntos por grupo al unirse.
- Toda lectura/escritura filtra por pertenencia al grupo.

### 6.3 Contratos

Estados: `open`, `in_progress`, `voting`, `fulfilled`, `cheating`, `expired`, `cancelled`.

Transiciones permitidas:

```
open        -> in_progress | cancelled | expired
in_progress -> voting | cancelled | expired
voting      -> fulfilled | cheating | cancelled
```

- Los estados finales no se reabren.
- Toda transición valida actor, estado origen, versión e idempotencia, y se escribe en auditoría encadenada.
- Un retado por contrato; el destinatario es una persona o el grupo, no ambos.
- La fecha límite y la fianza las define quien publica.
- Fianzas por defecto: 25, 50 o 100 puntos (configurables por grupo entre 10 y 200, hasta 5 opciones).
- Sin saldo suficiente: `402`. Saldo nunca negativo.
- Recompensa: texto libre de 3 a 80 caracteres, moderado, sin cantidades ni símbolos de dinero.
- Si vence sin evidencia: `expired`; la fianza pasa al retador.
- Hasta 3 evidencias por contrato; reemplazables antes del primer voto.

### 6.4 Evidencias y moderación

- Tipos: `photo` y `link`. NO se suben videos.
- Foto: JPEG, PNG o WebP; máximo 5 MB; validar magic bytes, reencodificar, quitar EXIF y no aceptar SVG.
- La imagen se analiza en memoria con Rekognition antes de almacenar. Solo la aprobada se sube a S3 con URL firmada temporal; la rechazada no se publica y puede quedar en cuarentena cifrada máximo 24 horas.
- Enlace: HTTPS obligatorio y dominio en lista permitida. El backend NO descarga enlaces; si usa oEmbed, valida dominio, bloquea IP privadas, limita redirecciones y aplica timeout.
- Dominios iniciales: `youtube.com`, `youtu.be`, `twitch.tv`, `clips.twitch.tv`, `medal.tv`, `streamable.com` (lista final pendiente, Q-005).
- Texto libre: moderación con Comprehend; toxicidad alta ≥ 0.7 se rechaza.
- Si la moderación no responde: estado `pending`, no visible, falla cerrada.
- Fotos en S3/CloudFront; borrado a los 30 días salvo Muro de la Fama.
- OCR opcional con `FEATURE_VISION_OCR=false`; nunca aprueba ni rechaza por sí solo.

### 6.5 Jurado y votación

- 3 jurados por defecto, configurable de 1 a 7; nunca el retador ni el retado.
- Un voto por jurado (`legit` o `cheat`), inmutable, con comentario opcional de hasta 140 caracteres.
- Consenso: 66 % de votos emitidos con quórum de al menos la mitad de los jurados asignados.
- Votación de 48 horas; una extensión de 24 horas; luego decide el administrador; si no decide en 48 horas, `cancelled` con devolución de fianza.
- Empate: decide el administrador o se asigna un jurado adicional una sola vez.
- Tarea programada cierra votaciones vencidas.

### 6.6 Puntos y compromisos

- Saldo por usuario y grupo, modificado solo mediante movimientos del libro dentro de una transacción.
- Tipos: `bond_lock`, `bond_release`, `bond_forfeit`, `reward`, `tournament_entry`, `tournament_prize`, `adjustment`.
- Movimientos inmutables; correcciones con movimientos compensatorios y auditoría.
- Compromisos con vistas “Debo” y “Me deben”; cumplimiento por acreedor o solicitud del deudor.

### 6.7 Torneos

- Entre grupos; el grupo se inscribe como equipo; no hay torneos individuales.
- 4, 8 o 16 grupos; eliminación directa con byes si no es potencia de dos.
- Cada partida es 1 contra 1 entre representantes; el resultado se reporta con foto moderada y lo confirma el rival o el organizador.
- La bolsa va completa al grupo campeón y se reparte entre los miembros que jugaron.

### 6.8 Reputación

Índice de Palabra global de 0 a 100, calculado de forma determinista desde eventos:

```
indice = clamp(0, 100,
    50
    + 4 * retos_cumplidos
    - 12 * trampas_confirmadas
    - 5 * retos_vencidos_sin_evidencia
    + 2 * compromisos_cumplidos_a_tiempo
    - 4 * compromisos_vencidos_mas_de_7_dias
    + min(10, votos_alineados_con_el_consenso))
```

La IA puede proponer otra fórmula; el equipo la ratifica y se documenta antes de reemplazarla.

---

## 7. Contrato de API

### 7.1 Convenciones

- Base: `/api/v1`.
- JSON UTF-8; payload máximo 1 MB.
- Respuesta exitosa: `{ "data": ..., "meta": ... }`.
- Error: `{ "code": "...", "message": "...", "request_id": "...", "details": ... }`.
- Códigos: 200, 201, 204, 400, 401, 402, 403, 404, 409, 422, 429, 503.
- Paginación por cursor (`limit` por defecto 20, máximo 50).
- `Idempotency-Key` obligatoria en aceptar reto, votar, inscribirse y mover puntos.
- Versionado en URL; OpenAPI actualizado en cada cambio.
- Sin API keys; autenticación JWT Bearer.
- Rate limiting por usuario e IP en Redis; estricto en login, IA, evidencias y reportes.

### 7.2 Endpoints prioritarios de la evaluación del 3 y 4 de noviembre

Estos siete endpoints son la compuerta de la Fase 2 y DEBEN estar publicados en la nube:

| # | Método y ruta | ID fuente | Regla clave |
|---|---|---|---|
| 1 | `POST /api/v1/auth/register` | USR-01 | Correo, contraseña y consentimiento; apodo temporal único; 201/409/422/429 |
| 2 | `POST /api/v1/auth/login` | AUTH-01 | Access JWT 15 min + refresh rotativo; bloqueo tras 5 fallos; 200/401/429 |
| 3 | `POST /api/v1/auth/logout` | AUTH-04 | Revoca refresh en MongoDB y lo agrega a Redis; `all=true` opcional; 204/401 |
| 4 | `POST /api/v1/groups` | GRP-01 | Grupo privado + código de invitación de 6 caracteres; creador admin; 201/422 |
| 5 | `POST /api/v1/contracts` | CON-01 | Reto con juego, condición, recompensa, fecha límite y fianza; 201/403/422 |
| 6 | `POST /api/v1/contracts/{id}/evidence` | EVI-02 | Foto moderada antes de S3 o enlace de dominio permitido; 201/422 |
| 7 | `GET /api/v1/tournaments` | TOR-02 / TOR-03 | Torneos exprés y brackets del grupo; 200 |

**Conflicto de rutas:** el CRUD define contratos como `/groups/{gid}/contracts` y torneos como `/groups/{gid}/tournaments`; la evaluación exige `/contracts` y `/tournaments`. Se mantienen ambas formas mediante rutas canónicas anidadas y alias de evaluación, con el `group_id` resuelto por pertenencia. Ver Q-001 y Q-011.

---

## 8. Seguridad mínima obligatoria

- OWASP ASVS nivel 2 y OWASP API Security Top 10.
- `deny by default`; RBAC por grupo, reto y torneo.
- Validación estricta de DTOs; rechazo de campos desconocidos; longitudes, tipos y rangos.
- Prevención de inyección NoSQL: rechazar claves que empiecen con `$` o contengan puntos; sin consultas crudas.
- Prevención de IDOR: verificar pertenencia al grupo y propiedad del objeto en cada consulta.
- Prevención de SSRF: no descargar enlaces de evidencia; validar dominios y bloquear IP privadas en oEmbed.
- JWT: algoritmo fijado, validación de issuer, audience, expiración y firma; sin `alg=none`.
- Refresh tokens: solo hash en base de datos; rotación con detección de reutilización.
- Rate limiting en Redis; límites locales de respaldo si Redis falla.
- Cabeceras de seguridad, HTTPS/TLS 1.2+, HSTS y CORS restringido.
- Secretos en AWS Secrets Manager o Parameter Store; rotación; IAM de mínimo privilegio; OIDC para GitHub Actions.
- S3 privado con bloqueo de acceso público; CloudFront con OAC; URLs firmadas de corta duración.
- Logs sin contraseñas, tokens, `Authorization`, correos completos, frases dictadas, URLs firmadas, claves, OCR, cuerpos de evidencia ni PII.
- Auditoría encadenada por hash, solo inserción, 12 meses, anonimizada al eliminar la cuenta.

---

## 9. Pruebas y DevSecOps

### 9.1 Proyectos y responsabilidad

| Proyecto | Responsabilidad | Herramientas |
|---|---|---|
| `ContractTest` | Contrato contra `api/openapi.yaml` y Pact | PactNet, xUnit |
| `DatabaseTest` | Índices, TTL, transacciones y repositorios con MongoDB real | Testcontainers.MongoDb |
| `IntegrationTest` | Flujo end-to-end por endpoint con Mongo y Redis reales | WebApplicationFactory, Testcontainers |
| `MutationTest` | Calidad de las pruebas | Stryker.NET, umbral 60 % |
| `PerformanceTest` | Carga ligera de la demo y límites M0 | NBomber |
| `SecurityTest` | Autorización, IDOR, JWT, rate limit, SSRF, NoSQL, archivos | xUnit, WebApplicationFactory |
| `UnitTest` | Máquina de estados, consenso, puntos, brackets, Índice de Palabra, validación de enlaces | xUnit, FluentAssertions, Moq |

### 9.2 Pipeline obligatorio

```
formato/lint -> build -> unitarias -> SAST -> SCA/SBOM -> build imagen
-> escaneo imagen -> integración -> contrato -> seguridad -> mutación (opcional)
-> compuerta de calidad -> terraform plan/apply -> despliegue 2 instancias
-> smoke -> DAST -> destrucción manual
```

- SAST: Semgrep, SonarCloud, analizadores .NET.
- SCA/SBOM: `dotnet list package --vulnerable`, Dependabot, CycloneDX.
- Secretos: GitHub secret scanning, Gitleaks.
- Contenedor: Trivy, Dockle.
- IaC: Checkov o tfsec.
- DAST: OWASP ZAP.
- Firma: Cosign con OIDC (opcional).
- Compuerta: bloquea el despliegue si hay hallazgos críticos o altos, pruebas rojas o cobertura por debajo del umbral acordado.
- Autenticación AWS por OIDC; sin llaves de largo plazo.
- Aprobación manual para desplegar y destruir.

### 9.3 Definición de terminado

Una funcionalidad está terminada solo si:

- Sus criterios de aceptación están cumplidos y verificados.
- Existe un expediente `specs/<ID>/` con `spec.md`, `plan.md`, `tasks.md` y `checklists/checklist.md`; el resultado global está en `specs/validation.md`.
- Pruebas unitarias, integración, contrato y seguridad en verde.
- Escaneos sin hallazgos críticos ni altos.
- OpenAPI actualizado.
- Logs, métricas y auditoría presentes.
- Otra persona revisó y el Product Owner aprobó.
- Versión etiquetada según la matriz.

---

## 10. Matriz de versiones

| Versión | Contenido | Sprint |
|---|---|---|
| 0.1.0 | Infraestructura base, cuentas, sesión y grupos | S2 |
| 0.2.0 | Contratos, aceptar reto y contrato por voz con IA | S3 |
| 0.3.0 | Evidencias, moderación, jurado y puntos | S4 |
| 0.4.0 | Torneos, compromisos, muros y push | S4 |
| 1.0.0 | Versión de evaluación | S5 |

Versionado semántico. Todo despliegue DEBE etiquetarse y registrar la versión en `Memoria.md`.

---

## 11. Flujo SDD y gobierno del cambio

Por expediente (`BE-xx` para la base o `USR/AUTH/GRP/CON/EVI/TOR` por endpoint):

1. **Especificar:** `spec.md` con contexto, fuera de alcance, requisitos EARS, endpoints con IDs, esquemas, errores, seguridad y criterios de aceptación.
2. **Planificar:** `plan.md` con diseño técnico, archivos, dependencias y riesgos.
3. **Dividir:** `tasks.md` con tareas pequeñas y verificables.
4. **Implementar:** código y pruebas por tarea.
5. **Verificar:** pruebas, escaneos, revisión y aprobación.

El índice vive en `specs/README.md`; las plantillas en `.specify/templates/spec/`; las ayudas de lectura en `.opencode/`.

Reglas del agente:

- Leer `constitution.md` y `AGENTS.md` antes de escribir código.
- No inventar requisitos; registrar dudas en `specs/open-questions.md`.
- Citar IDs en especificaciones, commits y pruebas.
- No tocar secretos ni agregar dependencias sin justificarlas.
- Escribir pruebas antes o junto al código.
- No subir cambios que rompan la compuerta de calidad.
- Actualizar `Memoria.md` al cerrar cada fase.

**Enmiendas:** cualquier cambio a esta constitución requiere justificación escrita, aprobación del Product Owner y actualización de `Memoria.md`. Las decisiones arquitectónicas se registran como ADR en `docs/adr/`.

---

## 12. Restricciones de costo y operación

- MongoDB Atlas M0: 512 MB, ~100 ops/s, 500 conexiones, sin respaldos ni SLA.
- Todas las consultas DEBEN usar índices; listas paginadas; proyecciones; pool de conexiones bajo.
- Caché obligatoria en lecturas frecuentes; Redis compartido entre instancias.
- Sin respaldos automáticos en M0: volcado y script de semillas en el repositorio antes de la evaluación.
- Recursos apagados o destruidos fuera de uso; AWS Budgets con alertas al 50, 80 y 100 %.
- Tiempo de recuperación objetivo durante la evaluación: 1 hora.
- Runbook de incidentes, rollback de despliegue y destrucción controlada.

---

## 13. Enmiendas

| Versión | Fecha | Cambio | Aprobó |
|---|---|---|---|
| 1.0.0 | 2026-10-30 | Constitución inicial de la Fase 1; .NET 8 fijado por directriz del proyecto | David (PO) |
