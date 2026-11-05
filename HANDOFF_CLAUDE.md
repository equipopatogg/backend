# Traspaso de contexto — PactoGG (backend + móvil)

> Escrito el 2026-11-04 al cerrar una sesión de trabajo con Claude (chat). Léelo completo antes de tocar código.
> Usuario: David Montoya Rodríguez (PO del equipo PueblOS, UTTT 10IDGS). Habla español; responde en español.

## 1. Qué es

PactoGG: red social privada para grupos de amigos gamers. Retos (contratos) con fianza de **puntos virtuales sin valor monetario**, evidencias (foto o enlace), jurado de amigos, torneos entre grupos, libro de compromisos. Nunca dinero ni vocabulario de apuestas.

Evaluación del docente: **2-dic-2026** (la app se descarga de Google Play). Revisión parcial del backend: 3-4 nov (ya pasó con el backend desplegado).

## 2. Dónde está todo

| Qué | Ruta |
|---|---|
| Backend .NET 8 | `G:\PactoGG` (WSL: `/mnt/g/PactoGG`, Git Bash: `/g/PactoGG`) → repo `github.com/equipopatogg/backend` |
| App Android | `G:\PactoGGMovil\Movil` |
| Documentos fuente (verdad del producto) | `C:\Users\david\Downloads\`: `CLARIFICACION.docx` (143 respuestas de reglas), `PactoGG — Descripción del Backend.docx`, `PactoGG — Descripción del Móvil.docx`, `CRUD_y_Endpoints_PactoGG.xlsx` (~90 endpoints con IDs), `Componentes_Backend_y_Movil_PactoGG.xlsx` (BE-xx, MV-xx, NB-xx, LP-xx), `Stack_Tecnologico_PactoGG.xlsx`, `Requerimientos_para_el_desarrollo_del_proyecto_M_vil_v3.pdf` |
| Guía de API para móvil | `G:\PactoGG\docs\API-movil.md` y contrato `G:\PactoGG\api\openapi.yaml` |
| SDD backend | `G:\PactoGG\specs\` (un expediente por endpoint), `Memoria.md`, `constitution.md`, `AGENTS.md` |

Prioridad de documentos ante conflicto: decisiones registradas en `Memoria.md` (p. ej. D-15) > CLARIFICACION > Descripciones > Excel.

## 3. Requisitos del docente que condicionan todo

- Backend en AWS **"configurado con costo cero"** (créditos permitidos; nada que cobre a la tarjeta), ≥2 instancias, load balancer o similar, SSL, Redis distribuido, CDN para imágenes, observabilidad, Terraform para crear y destruir, CI/CD, DevSecOps, JWT, logout en backend y móvil.
- Móvil: igual a la maqueta (se pueden agregar funciones, nunca quitar), sesión siempre viva salvo logout, eliminar cuenta, aviso de privacidad, permisos, push (FCM), SQLite offline, al menos 1 función de ML y 1 de IA generativa, moderación antes de almacenar, despliegue desde GitHub a Google Play (pruebas internas).
- Todo gratis excepto la licencia de Google Play.

## 4. Backend: estado actual (desplegado y verificado)

- **URL base:** `https://52-3-200-211.sslip.io` (rutas bajo `/api/v1`). `/health` → `Healthy`.
- **CDN imágenes:** `https://d3shxplwv8bmvp.cloudfront.net`
- **Endpoints implementados (8 de ~90):** `POST /auth/register` (USR-01), `POST /auth/login` (AUTH-01), `POST /auth/refresh` (AUTH-03), `POST /auth/logout` (AUTH-04), `POST /groups` (GRP-01), `POST /groups/{gid}/contracts` y alias `POST /contracts` (CON-01), `POST /contracts/{id}/evidence` (EVI-02, JSON link o multipart foto), `GET /groups/{gid}/tournaments` y alias `GET /tournaments?group_id=` (TOR-02).
- **Formato:** JSON snake_case. Éxito `{ "data": ..., "meta": {...} }`. Error `{ "code", "message", "request_id", "details" }`. Campos desconocidos → 400.
- **Sesión:** access JWT 15 min + refresh **rotativo** (guardar siempre el nuevo; reusar uno viejo = 401). Logout `{ "refresh_token": "..." }` o `{ "all": true }` → 204.
- **Registro:** `nickname` es **obligatorio** (decisión D-15/F2-D02 del PO; prevalece sobre CLARIFICACION #6). Regex `^[A-Za-z0-9_.-]{3,24}$`, password 12–128, `accept_privacy: true`, `privacy_version: "2026-10-01"`.
- Pruebas: 51 verdes, 1 omitida (Testcontainers). Usuario de prueba en prod: `prueba@pactogg.com` / `PruebaSegura123!`.

Decisiones recientes (ver `Memoria.md`):
- **F2-D19** infraestructura costo cero: 2 EC2 t3.micro (us-east-1a/b) con Elastic IP, **Caddy** en la instancia 1 como balanceador + TLS Let's Encrypt (`sslip.io`), Redis en contenedor en la instancia 1, S3 + CloudFront (OAC), CloudWatch Logs, SSM Parameter Store. Sin ALB/NAT/ElastiCache.
- **F2-D20** la primera evidencia pasa el contrato a `voting`; máx. 3 evidencias (409 `EVIDENCE_LIMIT_REACHED`).
- **F2-D21** `target_type` `group|member`; retarse a sí mismo → 422 `INVALID_TARGET`.
- **F2-D22** moderación de imágenes para videojuegos (`ImageModerationPolicy`, `Moderation:Image` en appsettings): armas permitidas; violencia gráfica solo si Rekognition marca `Animated`/`Illustrated`; explícito, odio, perturbador y autolesión siempre bloqueados; confianza 80 %.

### Cómo se opera el backend (WSL)

- Herramientas: `aws`, `terraform` en `~/.local/pactogg-tools/bin` o `/tmp/opencode/...` (ya en el PATH vía `~/.bashrc`), `dotnet` en `~/.dotnet`. Perfil AWS `pactogg` (usuario IAM `PactoGGadmin`, cuenta 267150804376), región `us-east-1`.
- Secretos locales (NO versionados): `deploy/terraform/.env.secrets` (URI de Atlas y clave JWT), `deploy/terraform/terraform.tfvars`. Para Terraform: `set -a; . ./.env.secrets; set +a; export TF_VAR_mongo_connection_string="$MONGO_URI" TF_VAR_jwt_signing_key="$JWT_SIGNING_KEY"`.
- Publicar versión nueva: `bash scripts/release.sh` (dotnet test → build/push ECR con reintentos → redeploy SSM instancia por instancia → smoke). `SKIP_TESTS=1` para omitir pruebas.
- Primer despliegue / recrear: `bash scripts/deploy-first-time.sh`. Destruir: `bash deploy/terraform/destroy.sh`.
- Instancias actuales: `i-0b9d4f3871782dd58` (gateway), `i-02ef5f19403170ffb` (worker). IPs a permitir en Atlas: `52.3.200.211`, `34.200.123.191`.

### Trampas ya resueltas (no repetir)

- La cuenta AWS se creó con "Sign up for AWS (new)": hay SCP de una organización administrada por AWS. Se activaron características avanzadas y el PO editó la SCP `p-pq3t9xd4` para permitir EC2/ECR/SSM/ec2messages/ssmmessages en us-east-1. Si algo da "explicit deny in a service control policy", es la SCP, no IAM.
- AWS rechaza apóstrofos en descripciones de reglas de security group.
- Las instancias dependen de las reglas de red (`depends_on`) para no arrancar sin egress.
- Docker Desktop a veces da "TLS handshake timeout" con ECR: reintentar.
- GitHub OIDC manda el `sub` con IDs: `repo:equipopatogg@349079173/backend@1436429805:ref:refs/heads/main`. El rol acepta ambos formatos (`github_repo_with_ids`).
- `aquasecurity/trivy-action` fue comprometido (GHSA-69fq-xp46-6x23); el pipeline usa `docker run aquasec/trivy:0.69.3`.
- La carpeta `.github` del backend estaba protegida para la herramienta del chat; se editó a mano.

### Pendientes del backend

1. Confirmar que `terraform apply` del OIDC con IDs se aplicó y que el pipeline `backend-ci-cd.yml` pasa completo (secret `AWS_ROLE_ARN`, variables `INSTANCE_IDS`, `PUBLIC_BASE_URL`, `AWS_REGION`, environment `production` ya configurados en GitHub).
2. Publicar F2-D22 si no se ha hecho (`bash scripts/release.sh`) y commitear.
3. Siguientes endpoints del flujo mínimo (CLARIFICACION #1): `GET/PATCH /users/me` (USR-02/03), `DELETE /users/me` (USR-04), `GET /users/check-nickname` (USR-06), `GET /groups` (GRP-02), `POST /groups/join` (MEM-01), `GET /groups/{id}/members` (MEM-02), `GET /groups/{gid}/contracts` (CON-02), `GET /contracts/{id}` (CON-03), `POST /contracts/{id}/accept|reject` (CON-06/07), votos (VOT-01..05), puntos (PTS-01/02), compromisos, dispositivos/FCM, contrato por voz con IA (CON-08). Flujo SDD: spec → plan → tasks → código + pruebas → validation.md → Memoria.md.
4. Tras la evaluación: rotar la contraseña de Atlas (estuvo en texto plano en `session-ses_e707.md`, que está fuera de Git) y apagar con `destroy.sh` cuando no se use.

## 5. App móvil: estado y tarea pendiente

Proyecto Kotlin + Jetpack Compose, MVVM/Clean, Hilt, Retrofit 3 + kotlinx.serialization, OkHttp, Room, DataStore, Coil 3. AGP 9.3.3, Gradle 9.5.0, JDK toolchain 21, compileSdk 36, minSdk 24. Paquete `com.pactogg.app`.

Lo que se sabe:
- Llegó por `.rar` desde la máquina de un compañero. `local.properties` apunta a `C:\Users\gmen2\AppData\Local\Android\Sdk` → **cambiar a** `C:\Users\david\AppData\Local\Android\Sdk` (existe). Probablemente también conviene borrar `.gradle/`, `.kotlin/`, `build/`, `app/build/` y `.idea/` (cachés de otra máquina) antes del primer Sync.
- David reporta que "no deja descargar Gradle"; falta el mensaje exacto. Revisar: `gradle/wrapper/gradle-wrapper.properties` (9.5.0 con `distributionSha256Sum`), `gradle/gradle-daemon-jvm.properties` (pide JDK 21 vía foojay), `settings.gradle.kts` (plugin foojay 1.0.0).
- La URL de la API se inyecta con la propiedad Gradle `pactogg.apiBaseUrl` (debe ser HTTPS y **terminar en `/`**): agregar en `gradle.properties` → `pactogg.apiBaseUrl=https://52-3-200-211.sslip.io/`. Llega como `BuildConfig.API_BASE_URL`.
- Código: muchas pantallas viven en `app/src/main/java/com/pactogg/app/prototype/` (AppRoot, HomePages, ManualChallengeScreen, TournamentPages, ReviewPages, etc.); `feature/*/presentation` tiene varias pantallas vacías y `feature/*/README.md` de módulos sin implementar (evidence, jury, notifications, settings, voice, fame). Ya existe `core/network/ApiClientFactory.kt`, `ApiErrorDto`, `ApiErrorMapper`, Room con `CacheMetadata`. **No hay todavía llamadas reales al backend.**
- Docs propios del móvil: `AGENTS.md`, `constitution.md`, `Memoria.md`, `DESIGN.md`, `specs/`, `.specify/`, `.opencode/`. Respetarlos (SDD).

Tarea pedida por David:
1. Hacer que el proyecto sincronice y compile en su PC (arreglar SDK/Gradle/JDK/cachés).
2. Conectar la app al backend real: registro, login, refresh silencioso (Authenticator de OkHttp con mutex), logout (backend + borrar datos locales), crear grupo, crear contrato, subir evidencia (link y foto multipart, manejar `422 CONTENT_REJECTED`), listar torneos. Tokens cifrados (DataStore/Keystore). Lo no disponible en el backend se deja con datos simulados detrás del repositorio, sin romper la UI.
3. Verificar que todo coincida con la Descripción del Móvil, la maqueta (tema siempre oscuro, poco texto, 1 botón principal naranja por pantalla, sin degradados/brillos), el Excel de componentes (MV-xx) y el SDD; documentar en `Memoria.md` y specs del móvil.
4. Pruebas: unitarias (ViewModel, validaciones, sesión), MockWebServer para repositorios.

## 6. Entorno de David

- Windows + WSL (Ubuntu) + Git Bash + Android Studio + Docker Desktop. Disco con poco espacio.
- En Git Bash usa la cuenta GitHub de equipo `equipopatogg` vía Git Credential Manager (selector de cuentas). `user.email` = `equipopatogg@gmail.com`.
- No subir nunca: `session-*.md`, `.env.secrets`, `*.tfvars`, `*.tfstate`, `local.properties`, CSV de claves AWS.
