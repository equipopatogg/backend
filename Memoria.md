# Memoria del proyecto — PactoGG Backend

## 0. Ficha

| Campo | Valor |
|---|---|
| Proyecto | PactoGG — backend de retos, grupos y torneos |
| Equipo | PueblOS · Grupo 10IDGS · UTTT |
| Cuatrimestre | Septiembre–diciembre 2026 |
| Evaluación | 3 y 4 de noviembre de 2026 (segunda unidad) |
| Entrega final | 2 de diciembre de 2026 |
| Stack | .NET 8 · ASP.NET Core Web API · MongoDB Atlas M0 · Redis · AWS · Docker · Terraform |
| Repositorio local | `G:\PactoGG` |
| Product Owner | David |
| Revisión técnica | Kevin |
| Estado actual | **Fase 2 implementada y verificada localmente** — 7 endpoints, SDD por endpoint, pruebas, Atlas local e imagen Docker; AWS/HTTPS pendientes |

---

## 1. Estado de la Fase 1

**Fecha de cierre:** 2026-10-30

### Completado

- Lectura y análisis de los documentos fuente:
  - `PactoGG — Descripción del Backend.docx`
  - `Requerimientos para el desarrollo del proyecto Móvil v3.pdf`
  - `Componentes_Backend_y_Movil_PactoGG.xlsx`
  - `CRUD_y_Endpoints_PactoGG.xlsx`
  - `Stack_Tecnologico_PactoGG.xlsx`
  - `PactoGG — Respuestas de clarificación revisadas (.NET y Kotlin).txt`
- Creación de `constitution.md` con principios, stack, dominio, seguridad, pruebas y definición de terminado.
- Creación de `AGENTS.md` con reglas operativas para agentes, comandos, mapa del repositorio y checklist de PR.
- Creación de `Memoria.md` (este documento).
- Creación de `specs/open-questions.md` con las contradicciones detectadas.
- Creación de la solución `Pactogg.sln` con 8 proyectos:
  - `Pactogg.Api` (Web API)
  - `ContractTest`
  - `DatabaseTest`
  - `IntegrationTest`
  - `MutationTest`
  - `PerformanceTest`
  - `SecurityTest`
  - `UnitTest`
- Creación de carpetas de gobierno: `specs/`, `docs/adr`, `api`, `deploy/terraform`, `.github/workflows`, `scripts`.
- Configuración base: `global.json`, `Directory.Build.props`, `nuget.config`, `.gitignore`, `appsettings.json`, `appsettings.Development.json`.
- API mínima con `/health`, Swagger en desarrollo, JSON camelCase, `AddControllers` y `public partial class Program` para pruebas de integración.

### Verificación ejecutada

- SDK .NET 8.0.427 instalado temporalmente en `/tmp/opencode/dotnet` para validar la solución.
- `dotnet restore Pactogg.sln`: correcto.
- `dotnet build Pactogg.sln -c Release --no-restore`: **0 errores y 0 advertencias** en la segunda pasada.
- `dotnet list package --include-transitive --vulnerable`: **sin vulnerabilidades** en los 8 proyectos.
- API ejecutada en `http://127.0.0.1:5199`: `GET /health` → 200, `GET /` → 200, `GET /swagger/v1/swagger.json` → 200.
- `dotnet test`: IntegrationTest 2/2 y SecurityTest 1/1 en verde. UnitTest, ContractTest y DatabaseTest aún no tienen casos; se agregarán en la Fase 2.

---

## 1.1 Estado de la Fase 2

**Fecha de verificación local:** 2026-11-02

### Completado

- Implementación de los siete endpoints de evaluación con JWT, Argon2id, MongoDB, Redis, moderación previa y S3.
- Rutas planas y alias anidados: `POST /api/v1/groups/{gid}/contracts` y `GET /api/v1/groups/{gid}/tournaments`.
- Rate limit Redis con fallback local; revocación de `jti`; bloqueo de login tras 5 fallos; refresh token hasheado con SHA-256.
- Moderación local determinista para desarrollo/pruebas y adaptador AWS para producción; en Production el arranque exige `Moderation:Mode=Aws`.
- Evidencias: enlace con allowlist HTTPS; foto con límite, decodificación, reencode JPEG, moderación antes de S3, máximo 3 y transición a `voting`.
- Torneos filtrados por grupos activos, bracket incluido y caché Redis de 30 segundos.
- Rechazo de claves JSON NoSQL y de campos desconocidos; errores con `code`, `message` y `request_id` sin stack traces en la respuesta.
- `api/openapi.yaml` con 9 rutas y 19 esquemas.
- Artefactos de despliegue: `Dockerfile` multi-stage no root, `deploy/docker-compose.aws.yml`, `deploy/Caddyfile`, Terraform para EC2/EIP/SG/IAM/S3 y `scripts/deploy-aws.sh` con `deploy/.env.example`.

### Verificación ejecutada

- `dotnet build Pactogg.sln -c Release`: **0 errores, 0 advertencias**.
- `UnitTest`: **25/25** en verde.
- `IntegrationTest`: **10/10** en verde.
- `SecurityTest`: **5/5** en verde.
- `ContractTest`: **2/2** en verde.
- Smoke HTTP: `/` 200, `/health` 200, Swagger 200, endpoint protegido 401, NoSQL 400.
- `api/openapi.yaml` parseado con PyYAML: 9 rutas, 19 esquemas.
- Detalle en `specs/validation.md`.

### Pendiente o bloqueado

- Docker no disponible en WSL en ese momento; la imagen se validó después con Docker Desktop (ver 1.2). Redis/Caddy locales siguen sin ejecutar.
- Terraform no instalado: falta `fmt`/`validate`.
- `DatabaseTest` con Testcontainers pendiente de habilitar la integración WSL de Docker.
- Sin credenciales AWS/MongoDB/Redis reales ni dominio: no se validó S3, Rekognition, Comprehend, HTTPS ni dos instancias.
- `dotnet list package --vulnerable --include-transitive` falló por timeout de NuGet; repetir con red estable o CI.
- Q-006, Q-007 y Q-012 siguen abiertas; no bloquean los siete endpoints.

---

## 1.2 Estado documental y de validación (2026-11-03)

### Completado

- Reestructuración SDD: `specs/README.md`, 11 expedientes (`BE-00` a `BE-03` y los siete endpoints), `.opencode` con agentes/skills de lectura, `.specify` con plantillas, `docs/endpoints.md` y `PREPARACION_SDD.md`.
- Las specs antiguas de `specs/features/core-endpoints` se consolidaron en expedientes por endpoint y se retiraron para evitar dos fuentes de verdad.
- `Program.cs` carga opcionalmente `appsettings.{Environment}.local.json` y `appsettings.Local.json`; Swagger solo se expone en Development/Testing.
- Atlas local configurado en `Pactogg.Api/appsettings.Development.local.json`, ignorado por Git. La conexión TLS y la verificación de índices funcionan desde este entorno; la API arranca y `/health` responde 200. Falta prueba E2E de endpoints contra Atlas y allowlist de las IPs NAT en AWS.
- Terraform: trust OIDC para `main` y `production`, egress HTTP/DNS, `kms:Decrypt` vía SSM y reintento de `docker pull` en user data.
- Pipeline con push opcional a DockerHub, además de ECR.
- Build Release sin errores; 49 pruebas verdes y 1 omitida; SCA sin vulnerabilidades; Terraform `fmt`/`validate` PASS; imagen Docker `pactogg-api:local` construida con Docker Desktop.

### Pendiente

- Revalidar build y pruebas tras los últimos cambios de `Program.cs`.
- Adjuntar `AdministratorAccess` (o una política de despliegue equivalente) al usuario IAM `PactoGGadmin`: autentica, pero `ec2:DescribeVpcs`, `s3:ListAllMyBuckets`, `route53:ListHostedZones` e `iam:GetUser` devuelven AccessDenied.
- Ejecutar `bash scripts/deploy-first-time.sh` en WSL; después permitir `gateway_public_ip` y `worker_public_ip` en Atlas. La cuenta (Sign up for AWS new) tenía SCP que bloqueaba EC2/ECR/ElastiCache; se activaron las características avanzadas el 2026-11-04. La SCP resultante bloqueaba servicios regionales en `us-east-1`; el PO ajustó la política de la organización y el despliegue se mantiene en `us-east-1`.
- Resolver Q-019 a Q-021 con el Product Owner.

---

## 2. Decisiones tomadas

| ID | Decisión | Justificación | Estado |
|---|---|---|---|
| F1-D01 | Framework **.NET 8 LTS** | Directriz explícita de esta fase y D-06 permite .NET 8 si el entorno del curso lo exige | Aprobada |
| F1-D02 | Nombre del proyecto principal `Pactogg.Api` | Coherencia con el nombre del producto y el `AGENTS.md` del equipo | Aprobada |
| F1-D03 | Solución clásica `Pactogg.sln` | Compatibilidad con .NET 8 SDK; `.slnx` requiere SDK más nuevo | Aprobada |
| F1-D04 | Proyectos de prueba en la raíz, como el repositorio de referencia | Facilita comandos y comparación con `WebAPIDevSecOpsScalling` | Aprobada |
| F1-D05 | Arquitectura monolito modular | D-07: un solo servicio con módulos; sin microservicios | Aprobada |
| F1-D06 | MongoDB con `IMongoDatabase` y driver oficial | Requisito NoSQL y D-02 | Aprobada |
| F1-D07 | Redis para caché, rate limit y tokens revocados | Requisito de sesión viva y logout backend | Aprobada |
| F1-D08 | Argon2id para contraseñas | Decisión del Product Owner en clarificaciones | Aprobada |
| F1-D09 | Sin video almacenado; evidencia foto o enlace | D-12 y clarificaciones | Aprobada |
| F1-D10 | Moderación antes de almacenar | Requisito del docente y del backend | Aprobada |
| F1-D11 | Rutas de evaluación planas y rutas canónicas anidadas | La evaluación exige `/contracts` y `/tournaments`; el CRUD define `/groups/{gid}/...` | Abierta (Q-001, Q-011) |
| F1-D12 | Paquetes endurecidos: MongoDB.Driver 3.13.0, Testcontainers 4.16.0, NBomber 6.7.1 / Http 6.2.1 | Eliminar vulnerabilidades transitivas altas detectadas por `dotnet list package --vulnerable` | Aprobada |
| F1-D13 | API base con `/health`, Swagger, request-id y cabeceras de seguridad | Permite verificar el despliegue y sirve de esqueleto para los módulos | Aprobada |
| F2-D01 | Implementar los 7 endpoints de evaluación en rutas planas | Instrucción directa del Product Owner del 2026-10-30; expedientes por endpoint en `specs/README.md` | Aprobada |
| F2-D02 | Apodo obligatorio en registro (D-15) | Instrucción directa del PO; prevalece sobre el apodo temporal de Q-002 | Aprobada |
| F2-D03 | Contraseña 12–128 con Argon2id (D-16) | Constitución §6.1 y Q-003 | Aprobada |
| F2-D04 | Evidencia fotográfica en una llamada multipart moderada antes de S3 (D-17) | Instrucción del PO y Q-013; simplifica el flujo de evaluación | Aprobada |
| F2-D05 | Enlaces solo con validación de dominio, sin descarga ni S3 (D-18) | Constitución §6.4 y Q-004 | Aprobada |
| F2-D06 | Logout `204` (D-19) | CRUD AUTH-04 y Q-008 | Aprobada |
| F2-D07 | `GET /tournaments` limitado a grupos del usuario (D-20) | Q-010/Q-014 y aislamiento por grupo | Aprobada |
| F2-D08 | Consentimiento con versión vigente configurable (D-21) | Q-015 y Constitución §6.1 | Aprobada |
| F2-D09 | Fianzas 25/50/100 configurables hasta 5 opciones 10–200 (D-22) | Q-016 y Constitución §6.3 | Aprobada |
| F2-D10 | Mantener alias anidados para contratos y torneos | Q-001/Q-010/Q-011 y AGENTS.md; el `group_id` de la ruta prevalece y se valida pertenencia | Aprobada |
| F2-D11 | Caché Redis de 30 s para listados de torneos | T-32 y reducción de carga en MongoDB Atlas M0 | Aprobada |
| F2-D12 | Producción exige `Moderation:Mode=Aws` | Falla cerrada y plan §2.4; local solo para desarrollo/pruebas | Aprobada |
| F2-D13 | Rechazar campos JSON desconocidos | AGENTS.md §6 y plan §2.6; `UnmappedMemberHandling.Disallow` | Aprobada |
| F2-D14 | Recompensa 3–80 sin dinero ni símbolos monetarios | Spec R-CON-05 y checklist de dominio | Aprobada |
| F2-D15 | SDD con un expediente por endpoint y ayudas de lectura en `.opencode`/`.specify` | Instrucción directa del PO del 2026-11-03 | Aprobada |
| F2-D16 | Secretos locales solo en `appsettings.*.local.json` ignorados | Constitución §7 y AGENTS §2 | Aprobada |
| F2-D17 | Swagger solo en Development/Testing | Evitar exposición de la superficie de API en producción | Aprobada |
| F2-D18 | ~~HTTPS con CloudFront delante del ALB~~ — reemplazada por F2-D19 | PO pidió inventar dominio | Sustituida |
| F2-D20 | La primera evidencia pasa el contrato a `voting`; hasta 3 evidencias mientras no haya votos | CLARIFICACION #30, #59, #60 (resuelve Q-017) | Aprobada |
| F2-D22 | Moderación de imágenes para videojuegos: armas permitidas; violencia gráfica permitida solo en capturas `Animated`; explícito, odio, perturbador y autolesión siempre bloqueados; confianza 80 % | CLARIFICACION #63 y uso real (capturas de shooters) | Aprobada |
| F2-D21 | `target_type` `group|member`; retarse a sí mismo devuelve 422 `INVALID_TARGET` | CLARIFICACION #32, #73 (resuelve Q-018) | Aprobada |
| F2-D19 | Despliegue de costo cero: 2 EC2 t3.micro, Caddy como balanceador + Let's Encrypt (`sslip.io`), Redis en contenedor, S3 + CloudFront para imágenes, CloudWatch Logs; sin NAT/ALB/ElastiCache | Requisitos del docente v3: "configurado con costo cero", 2 instancias, LB o similar, SSL, CDN | Aprobada |
| F2-D23 | Aceptar un reto (CON-06) es una transacción MongoDB: `open→in_progress` con versión, descuento de fianza solo si alcanza y `bond_lock` en `points_ledger`; 402 `INSUFFICIENT_POINTS` | Constitución §6.3/§6.6 y CLARIFICACION (fianza al aceptar, saldo nunca negativo, libro inmutable) | Aprobada |
| F2-D24 | Rechazar (CON-07) deja el reto en `cancelled` con `closed_reason=rejected`; no hay estado `rejected` | Máquina de estados de la Constitución §6.3 (`open→cancelled`) | Aprobada |
| F2-D25 | Caché por versiones: llaves con versión por usuario y por grupo; invalidar = cambiar versión. Perfil 60 s, mis grupos 60 s, miembros 60 s, tablero 30 s; detalle de grupo y de reto sin caché para que saldo y estado sean exactos | CRUD (cachés por endpoint) y Redis de costo cero | Aprobada |
| F2-D26 | Unirse: máximo 20 miembros por grupo (`Auth:MaxMembersPerGroup`) con incremento atómico; 409 `ALREADY_MEMBER`/`GROUP_FULL`/`GROUP_LIMIT_REACHED`; 10 intentos cada 10 min | Constitución §6.2 y CRUD MEM-01 | Aprobada |
| F2-D27 | `check-nickname` público con 3–24 caracteres (igual que el registro, D-15) y 20 consultas/min por IP; el Excel decía 3–20 | Coherencia con USR-01 | Aprobada |
| F2-D28 | El retador no puede aceptar su propio reto: 409 `CANNOT_ACCEPT_OWN_CONTRACT` | CRUD CON-06 ("no puede aceptar su propio reto", respuestas 200/402/409) | Aprobada |
| F2-D29 | Jurados al pasar a `voting`: al azar entre miembros activos, sin retador ni ejecutor, `settings.jurors` (3 por defecto); 48 h para votar | Constitución §6.5 y backlog ID 87/88 | Aprobada |
| F2-D30 | Consenso sobre jurados asignados (`votos de un lado / jurados ≥ consenso`); si todos votan sin consenso, vence el plazo o no hay jurados, decide un admin (VOT-04) | Constitución §6.5; simplifica el quórum para la evaluación | Aprobada |
| F2-D31 | Cierre transaccional: `fulfilled` → `bond_release` al que aceptó + compromiso retador→ejecutor + racha; `cheating` → `bond_forfeit` al retador + `cheated` | Backlog ID 99/100, Constitución §6.6 y CLARIFICACION (libro inmutable) | Aprobada |
| F2-D32 | Avisos dentro de la app (colección `notifications`, TTL 90 días) como mejor esfuerzo; push FCM queda para después | Backlog ID 69 ("primero aviso dentro de la app") e ID 143 | Aprobada |
| F2-D33 | Muro de la Fama = retos `fulfilled` con fotos/clips aprobados; resumen y resultado de votación sin caché | Backlog ID 132; exactitud del estado de votación | Aprobada |
| F2-D34 | Flujo Git con ramas `feature/*` y Pull Request a `main`; el pipeline prueba los PR y solo despliega al fusionar en `main` | Instrucción del PO (buenas prácticas en GitHub) | Aprobada |
| F2-D35 | Login con Google (ID 12): validación propia del ID token con las llaves públicas de Google; vincula por correo verificado; cuenta nueva exige apodo y privacidad; sin Client ID → 503 | Sprint 2 del plan y D-15 | Aprobada |
| F2-D36 | Crear/eliminar infraestructura desde el pipeline (`infra.yml`) con estado en S3 y bloqueo nativo; bootstrap separado (bucket, OIDC, rol de Terraform) | Plan del sprint 2 ("crear y eliminar desde el pipeline") y requisitos v3 | Aprobada |
| F2-D37 | Pipeline con Semgrep, Dockle y compuerta `quality-gate` además de CodeQL y Trivy | Plan del sprint 2 ("Semgrep, Trivy, Dockle y compuerta") | Aprobada |
| F2-D38 | Script de la BD `database/mongo-init.js` con validadores en modo `warn` | Plan del sprint 2 ("Script de la BD en el repositorio") | Aprobada |
| F2-D39 | La imagen Docker excluye `appsettings.*.local.json` y otros secretos locales | Revisión de seguridad: el build local podía meter la cadena de Atlas en la imagen | Aprobada |
| F2-D40 | Los documentos (CLARIFICACION, Descripción del Backend, Stack, Componentes, CRUD, Requerimientos) mandan sobre el Sprint Planning | Instrucción del PO del 2026-11-05 | Aprobada |
| F2-D41 | Consenso sobre votos emitidos con quórum de la mitad; extensión única de 24 h; fase del administrador de 48 h y cancelación con devolución; empate por admin no parte o jurado adicional único; tarea programada `ContractDeadlineWorker` cada 5 min | CLARIFICACION #79–84 y DESC_BACK 7.6 | Aprobada |
| F2-D42 | Reto cumplido: libera fianza + premio igual (`reward`); vencido sin evidencia → `expired` y fianza al retador; mínimo 3 miembros para lanzar retos; jurados 1–7 y consenso 0.50–1.0; votos revelados anónimos | CLARIFICACION #22, #36, #72, #75, #84, #89 | Aprobada |
| F2-D43 | Idempotency-Key obligatoria en aceptar, votar y resolver (Redis 24 h); detección de reutilización del refresh (revoca todas las sesiones); JSON máximo 1 MB; gzip fuera de `/auth`; avisos con retención de 30 días y clave de deduplicación | DESC_BACK 9, CLARIFICACION #10, #118–119 | Aprobada |
| F2-D44 | Pipeline: Gitleaks, Checkov, SBOM CycloneDX, SonarCloud (se activa con `SONAR_TOKEN`), OWASP ZAP tras el smoke test, Dependabot y acciones fijadas por SHA. Bootstrap: AWS Budgets 50/80/100 % (neto y bruto) y CloudTrail siempre activo | CLARIFICACION (CI/CD, #131–132) y DESC_BACK 12 | Aprobada |
| F2-D46 | Correo por SMTP (SES o el correo del proyecto) con `System.Net.Mail`; sin host configurado no se envía y `Auth:RequireVerifiedEmail` queda en `false` hasta configurarlo en producción | NuGet no tiene paquete nuevo disponible en este entorno; SES por SMTP cumple CLARIFICACION #5 | Aprobada |
| F2-D47 | Tokens de verificación y recuperación en Redis (solo SHA-256, un solo uso, 24 h / 1 h) | CLARIFICACION seguridad: token de un solo uso, hasheado y con caducidad | Aprobada |
| F2-D48 | Push con FCM HTTP v1 sin SDK (JWT RS256 de la cuenta de servicio), cola en memoria y preferencias por categoría | Evita una dependencia nueva; el aviso queda en el centro de notificaciones aunque el push esté apagado | Aprobada |
| F2-D49 | Solo un refresh **rotado** que reaparece revoca todas las sesiones; los revocados por logout o cambio de contraseña solo dan 401 | Evita cerrar la sesión nueva tras cambiar la contraseña (CLARIFICACION #10) | Aprobada |
| F2-D45 | La numeración de versiones ya publicada (0.4–0.6) no se reescribe; la matriz 0.1–0.4 del documento se cubre por contenido, no por número | Decisión del PO del 2026-11-05 | Aprobada |

### 2.1 Registro de aprobación de la Fase 2

- **Aprobador:** David, Product Owner.
- **Fecha:** 2026-10-30.
- **Medio:** instrucción directa en la sesión de trabajo.
- **Alcance:** siete endpoints, rutas planas, apodo obligatorio, moderación previa, S3, pruebas y despliegue AWS/HTTPS.
- **Documento aprobado:** `specs/README.md` y los expedientes por endpoint; el plan histórico se consolidó en esa estructura.
- **Condición:** cambios posteriores de comportamiento exigen actualizar `spec.md` y registrar la decisión.

---

## 3. Contradicciones y decisiones pendientes

Registradas en `specs/open-questions.md`:

- **Q-001:** resuelta (D-14): ruta plana con `group_id` en el cuerpo y alias anidado.
- **Q-002:** resuelta (D-15): apodo obligatorio en el registro.
- **Q-003:** resuelta (D-16): 12–128, Argon2id.
- **Q-004:** resuelta (D-18): enlaces validados por dominio; solo fotos van a S3.
- **Q-005:** resuelta (D-18): allowlist configurable con host exacto, `www.` y `m.`.
- **Q-006:** resuelta (F2-D19): Redis en contenedor compartido.
- **Q-007:** abierta: proveedor gratuito de IA generativa.
- **Q-008:** resuelta (D-19): `204`.
- **Q-009:** resuelta a .NET 8 en F1-D01.
- **Q-010:** resuelta (D-20): `GET /tournaments` por grupos del usuario.
- **Q-011:** resuelta (D-14/D-20): `group_id` verificado por pertenencia.
- **Q-012:** abierta: oEmbed apagado en esta entrega.
- **Q-013:** resuelta (D-17): una llamada multipart.
- **Q-014:** resuelta (D-20): alcance por grupos del usuario.
- **Q-015:** resuelta (D-21): versión vigente configurable.
- **Q-016:** resuelta (D-22): 25/50/100 configurables.
- **Q-017:** resuelta (F2-D20): `voting` con la primera evidencia; hasta 3 mientras no haya votos.
- **Q-018:** resuelta (F2-D21): `group|member`; auto-reto prohibido (422).
- **Q-019:** abierta: longitudes de apodo y nombre de grupo.
- **Q-020:** abierta: igualar trabajo de hash en login para correo inexistente.
- **Q-021:** abierta: moderación del nombre de grupo.

Las preguntas Q-007, Q-012 y Q-019 a Q-021 siguen abiertas; no bloquean los siete endpoints.

---

## 4. Endpoints prioritarios para la evaluación

| # | Endpoint | ID | Estado |
|---|---|---|---|
| 1 | `POST /api/v1/auth/register` | USR-01 | Implementado y probado localmente |
| 2 | `POST /api/v1/auth/login` | AUTH-01 | Implementado y probado localmente |
| 3 | `POST /api/v1/auth/logout` | AUTH-04 | Implementado y probado localmente |
| 4 | `POST /api/v1/groups` | GRP-01 | Implementado y probado localmente |
| 5 | `POST /api/v1/contracts` | CON-01 | Implementado y probado localmente |
| 6 | `POST /api/v1/contracts/{id}/evidence` | EVI-02 | Implementado y probado localmente |
| 7 | `GET /api/v1/tournaments` | TOR-02/TOR-03 | Implementado y probado localmente |

**Jurado, puntos, compromisos, avisos y fama (v0.6.0, 2026-11-04):** EVI-03, VOT-01 a VOT-05, PTS-01, PTS-02, COM-01, COM-03, COM-04, NOT-01 a NOT-03 y ACH-01. Nuevas colecciones `votes`, `commitments` y `notifications`; pruebas en `IntegrationTest/JuryFlowTests.cs` (cumplido, trampa, empate y acceso ajeno). 50 pruebas de integración/seguridad/contrato en verde.

**Endpoints sociales (v0.5.0, 2026-11-04):** USR-02 `GET /users/me`, USR-03 `PATCH /users/me`, USR-06 `GET /users/check-nickname`, GRP-02 `GET /groups`, GRP-03 `GET /groups/{id}`, MEM-01 `POST /groups/join`, MEM-02 `GET /groups/{id}/members`, CON-02 `GET /groups/{gid}/contracts`, CON-03 `GET /contracts/{id}`, CON-06 `POST /contracts/{id}/accept`, CON-07 `POST /contracts/{id}/reject`. Implementados con expediente en `specs/`, OpenAPI y pruebas (`IntegrationTest/SocialFlowTests.cs`, `SecurityTest`); 31 pruebas de integración/seguridad/contrato en verde. Nueva colección `points_ledger` con índices `ux_points_ledger_ref_type_user` e `ix_points_ledger_user_group_date`.

La implementación local está completa, con pruebas verdes, Atlas conectado e imagen Docker construida. El despliegue en AWS con HTTPS sigue pendiente de permisos de despliegue, dominio y ejecución de Terraform.

---

## 5. Arquitectura objetivo

```
App Android (Kotlin)
        |
        | HTTPS
        v
Instancia 1 (EC2 t3.micro, AZ a)            Instancia 2 (EC2 t3.micro, AZ b)
  Caddy :443 (Let's Encrypt, sslip.io)  ---->  API :8080
    |  round-robin + /health                       |
    +--> API :8080                                 |
  Redis (contenedor) <-----------------------------+
        |
        |
  +-----+------+--------+---------+----------+
  |            |        |         |          |
MongoDB      Redis    S3/CloudFront  Rekognition  CloudWatch
Atlas M0              (fotos)        Comprehend   SSM Parameter Store
```

- Costo cero (F2-D19): sin ALB, NAT ni ElastiCache; todo cubierto por créditos de AWS.
- Backend sin estado local; caché y revocación en Redis compartido.
- Fotos moderadas en memoria antes de S3; enlaces solo validados por dominio.
- Auditoría encadenada y libro de puntos inmutable.
- Terraform crea y destruye; GitHub Actions con OIDC.

---

## 6. Riesgos principales

| Riesgo | Impacto | Mitigación |
|---|---|---|
| SDK .NET 8 no instalado de forma permanente | Bloquea compilar en equipos nuevos | Respetar `global.json`, instalar SDK 8 y validar en CI |
| Tiempo hasta la evaluación del 3–4 de noviembre | Alto | Priorizar los 7 endpoints y el despliegue |
| MongoDB M0 sin respaldos | Pérdida de datos | Script de semillas y volcado antes de la evaluación |
| Rutas contradictorias | Retrabajo | Resolver Q-001/Q-010 con el PO antes de implementar |
| Moderación externa caída | Evidencias no disponibles | Falla cerrada, estado `pending`, reintento |
| Créditos AWS limitados | Servicios apagados | Apagar/destruir fuera de uso; AWS Budgets |
| Proveedor de IA no fijado | Bloqueo del contrato por voz | Adaptador y fallback manual; Q-007 |
| Redis único punto de fallo | Rate limit/caché degradados | Fallback local y reconexión; no degradar autorización |

---

## 7. Próximas fases

### Fase 2 — Núcleo evaluable (completada localmente)

La implementación, las specs por endpoint y la validación local están completas. Queda la ejecución real:

1. Permitir `gateway_public_ip` y `worker_public_ip` en MongoDB Atlas; verificar conexión y semillas desde el despliegue.
2. Publicar la imagen en ECR y ejecutar Testcontainers/Redis locales.
3. Ejecutar `bash scripts/deploy-first-time.sh` (costo cero; dominio gratis con sslip.io).
4. Ejecutar el workflow, el smoke público y la verificación de dos instancias.
5. Resolver Q-019 a Q-021 y actualizar OpenAPI/pruebas si cambia el comportamiento.

### Fase 3 — Reglas completas

- Jurado, votación, consenso y cierre programado.
- Puntos, fianzas, compromisos y auditoría encadenada.
- Contrato por voz con IA y adaptador.
- Notificaciones push y centro de notificaciones.
- Reputación e Índice de Palabra.

### Fase 4 — Calidad y DevSecOps

- ContractTest con Pact, DatabaseTest con Testcontainers, PerformanceTest con NBomber.
- SAST, SCA, SBOM, Trivy, Dockle, Checkov/tfsec, ZAP.
- Terraform completo y destrucción controlada.
- MutationTest con umbral 60 % si el tiempo lo permite.

### Fase 5 — Cierre

- OpenAPI final, Postman, runbook, volcado y semillas.
- Validación de los 7 endpoints en la nube.
- Actualización de `validation.md` y esta memoria.

---

## 8. Entorno y verificación

- **Sistema actual:** Linux (WSL) con acceso a `G:\PactoGG`.
- **SDK .NET:** se instaló .NET SDK 8.0.427 de forma temporal en `/tmp/opencode/dotnet` para validar esta fase. En este WSL falta ICU, así que la verificación usa `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`; el equipo debe instalar .NET 8 SDK en Windows o usar CI.
- **Documentos fuente:** analizados desde `/mnt/c/Users/david/Downloads/`.
- **Docker:** Docker Desktop responde por `docker.exe` desde WSL; la integración WSL nativa no está habilitada. La imagen `pactogg-api:local` compila correctamente.
- **Estado:** restore, build, ejecución de la API, pruebas de Fase 2, conexión Atlas e imagen Docker verificados localmente; documentación SDD reestructurada por endpoint. Terraform completo validado con `fmt`/`validate`; push a ECR, despliegue AWS/HTTPS y smoke público pendientes.

Comandos de verificación:

```bash
dotnet --info
dotnet restore Pactogg.sln
dotnet build Pactogg.sln -c Release --no-restore
dotnet test UnitTest/UnitTest.csproj -c Release --no-build
dotnet test IntegrationTest/IntegrationTest.csproj -c Release --no-build
dotnet test SecurityTest/SecurityTest.csproj -c Release --no-build
dotnet test ContractTest/ContractTest.csproj -c Release --no-build
dotnet test DatabaseTest/DatabaseTest.csproj -c Release --no-build
dotnet test MutationTest/MutationTest.csproj -c Release --no-build
dotnet test PerformanceTest/PerformanceTest.csproj -c Release --no-build
dotnet run --project Pactogg.Api/Pactogg.Api.csproj
cd deploy/terraform && terraform fmt -check -recursive && terraform validate
```

---

## 9. Bitácora de cambios

| Fecha | Versión | Cambio | Autor |
|---|---|---|---|
| 2026-10-30 | 0.1.0 | Fase 1: constitución, AGENTS, memoria, preguntas abiertas y solución base con 8 proyectos | Agente OpenCode / PO David |
| 2026-10-30 | 0.1.1 | Verificación de restore, build, API y auditoría de dependencias; paquetes endurecidos sin CVEs altas | Agente OpenCode |
| 2026-11-02 | 0.2.0 | Fase 2: siete endpoints, auth/grupos/contratos/evidencias/torneos, moderación, S3, OpenAPI, Docker/Terraform y 42 pruebas en verde | Agente OpenCode |
| 2026-11-02 | 0.3.0 | Terraform AWS completo en `deploy/terraform`, pipeline `backend-ci-cd.yml`, pruebas funcionales en los 7 proyectos y moderación antes de S3; 49 pruebas en verde y 1 omitida | Agente OpenCode |
| 2026-11-05 | 0.8.0 | Adelanto del sprint 3: verificación de correo, recuperación y cambio de contraseña, perfil público, aviso de privacidad, editar/cancelar retos, auditoría del grupo, borrar avisos, dispositivos y push con FCM; 96 pruebas; F2-D46 a F2-D49 | Claude / PO David |
| 2026-11-05 | 0.7.0 | Alineación con los documentos: reglas de votación y plazos, expiración, premio por reto cumplido, mínimo 3 miembros, idempotencia, reutilización de refresh, gzip, JSON 1 MB, avisos 30 d sin duplicados, pipeline (Gitleaks, Checkov, SBOM, SonarCloud, ZAP, Dependabot, SHA) y Budgets/CloudTrail; F2-D40 a F2-D45 | Claude / PO David |
| 2026-11-04 | 0.6.1 | Cierre del sprint 2 según el plan nuevo: login con Google, Semgrep + Dockle + compuerta, `infra.yml` (crear/eliminar), bootstrap de estado remoto, script de la BD, SDD 001–003 y plan de pruebas; F2-D35 a F2-D39 | Claude / PO David |
| 2026-11-04 | 0.6.0 | 15 endpoints de jurado, votación con cierre transaccional de fianza, puntos (saldo y ranking), compromisos, avisos en la app y Muro de la Fama; decisiones F2-D29 a F2-D34; flujo de ramas y PR | Claude / PO David |
| 2026-11-04 | 0.5.0 | 11 endpoints: perfil (ver/editar/apodo libre), mis grupos, detalle, unirse con código, miembros, tablero de retos, detalle de reto, aceptar con fianza transaccional y `points_ledger`, rechazar; caché por versiones; decisiones F2-D23 a F2-D28 | Claude / PO David |
| 2026-11-04 | 0.4.2 | Política de moderación de imágenes para videojuegos (F2-D22) con pruebas unitarias; configurable en `Moderation:Image` | Claude / PO David |
| 2026-11-04 | 0.4.1 | Q-017 y Q-018 resueltas con CLARIFICACION: `voting` con la primera evidencia y auto-reto prohibido; specs, OpenAPI y pruebas actualizadas; documentación alineada a costo cero | Claude / PO David |
| 2026-11-04 | 0.4.0 | Infraestructura de costo cero (F2-D19): 2 EC2 + Caddy LB/TLS + Redis en contenedor + CDN CloudFront para S3; redespliegue por SSM en script y pipeline | Claude / PO David |
| 2026-11-03 | 0.3.2 | Modo HTTPS sin dominio: CloudFront delante del ALB (prefix list + cabecera secreta), `domain_name`/`hosted_zone_id` opcionales, salidas `nat_public_ip`/`https_mode` y script `scripts/deploy-first-time.sh` de despliegue completo | Claude / PO David |
| 2026-11-03 | 0.3.1 | SDD por endpoint (11 expedientes), agentes/skills de lectura, plantillas `.specify`, conexión TLS e índices de Atlas verificados, build Docker y validación de Terraform/pipeline; Q-017 a Q-021 registradas | Agente OpenCode / PO David |

---

## 10. Regla de actualización

Al cerrar cada fase, cada decisión relevante o cada cambio de alcance:

1. Actualiza la tabla de estado y decisiones.
2. Registra riesgos nuevos.
3. Actualiza la bitácora.
4. Enlaza la spec, el plan y las tareas afectadas.
5. No borres decisiones anteriores; márcalas como reemplazadas con fecha.
