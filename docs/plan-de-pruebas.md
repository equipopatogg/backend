# Plan de pruebas y casos de prueba — PactoGG Backend

**Versión:** 0.6.1 · **Fecha:** 2026-11-04 · **Responsable de calidad:** José G. · **Alcance:** API `Pactogg.Api` (34 endpoints + `/health`).

## 1. Estrategia

| Nivel | Proyecto | Qué cubre | Herramienta | Cuándo corre |
|---|---|---|---|---|
| Unitarias | `UnitTest` | Reglas puras: recompensa sin dinero, enlaces permitidos, política de moderación de imágenes, procesado de imágenes, Argon2, Google sin configurar | xUnit + FluentAssertions | Cada PR y push |
| Integración | `IntegrationTest` | Flujos HTTP completos con la API en memoria (`WebApplicationFactory`) y repositorios falsos | xUnit | Cada PR y push |
| Seguridad | `SecurityTest` | 401 sin token o con token falso en todas las rutas protegidas; cabeceras de seguridad | xUnit | Cada PR y push |
| Contrato | `ContractTest` | `api/openapi.yaml` y Swagger documentan las rutas | xUnit | Cada PR y push |
| Base de datos | `DatabaseTest` | Índices en un MongoDB real (Testcontainers) | xUnit + Docker | Si hay Docker |
| Mutación | `MutationTest` | Configuración de Stryker.NET (umbral pendiente) | Stryker | Manual |
| Rendimiento | `PerformanceTest` | Prueba base del flujo (carga real pendiente) | xUnit | Cada PR |
| SAST | — | CodeQL y Semgrep (C#, secretos, OWASP) | GitHub Actions | Cada PR |
| SCA / secretos | — | NuGet vulnerable, Trivy filesystem | GitHub Actions | Cada PR |
| Imagen | — | Trivy image, Dockle (CIS) | GitHub Actions | Cada PR |
| Smoke | — | `GET /health` tras desplegar | GitHub Actions | Push a `main` |

**Criterio de salida:** 0 pruebas en rojo, 0 hallazgos CRITICAL/HIGH corregibles, 0 hallazgos Semgrep ERROR, 0 Dockle FATAL (compuerta `quality-gate`).

**Datos de prueba:** sintéticos (correos `user-<guid>@example.com`, apodos aleatorios). Nunca datos reales ni la base de producción.

## 2. Casos de prueba

### Cuentas y sesión (002)

| ID | Caso | Entrada | Resultado esperado | Automatizado en |
|---|---|---|---|---|
| CP-01 | Registro válido | correo nuevo, contraseña 12+, apodo, privacidad vigente | 201 con access y refresh | `CoreFlowTests` |
| CP-02 | Registro con campo desconocido | `{ ..., "$where": "1" }` | 400 `INVALID_INPUT` | `CoreFlowTests` |
| CP-03 | Login correcto / incorrecto | credenciales | 200 / 401 `INVALID_CREDENTIALS` | `FunctionalFlowTests` |
| CP-04 | Logout y reutilizar token | access revocado | 401 | `CoreFlowTests` |
| CP-05 | Google primera vez sin apodo | ID token válido | 422 `NICKNAME_REQUIRED` con correo | `GoogleAuthTests` |
| CP-06 | Google con apodo y privacidad | ID token + apodo | 201; segunda vez 200 | `GoogleAuthTests` |
| CP-07 | Google vincula correo existente | correo ya registrado | 200 con la misma cuenta | `GoogleAuthTests` |
| CP-08 | Google token falso / correo sin verificar | token inválido | 401 `INVALID_GOOGLE_TOKEN` | `GoogleAuthTests` |
| CP-09 | Google sin Client ID configurado | — | 503 `GOOGLE_AUTH_UNAVAILABLE` | `GoogleTokenValidatorTests` |
| CP-10 | Apodo libre / ocupado / inválido | `check-nickname` | true / false / 422 | `SocialFlowTests` |
| CP-11 | Cambiar apodo a uno ocupado o moderado | `PATCH /users/me` | 409 / 422 `CONTENT_REJECTED` | `SocialFlowTests` |

### Grupos y puntos (003)

| ID | Caso | Resultado esperado | Automatizado en |
|---|---|---|---|
| CP-20 | Crear grupo | 201 admin, 100 pts, código de 6 | `CoreFlowTests` |
| CP-21 | Unirse con código en minúsculas | 200 member, 100 pts, member_count 2 | `SocialFlowTests` |
| CP-22 | Unirse dos veces / código inexistente / formato inválido | 409 / 404 / 422 | `SocialFlowTests` |
| CP-23 | Grupo con 20 miembros | 409 `GROUP_FULL` | `SocialFlowTests` |
| CP-24 | Ajeno consulta grupo, miembros, puntos | 403 | `SocialFlowTests`, `JuryFlowTests` |
| CP-25 | Paginación de miembros con cursor | `has_more` y `next_cursor` correctos | `SocialFlowTests` |
| CP-26 | Saldo y movimientos; tipo inventado | lista del libro; 422 | `JuryFlowTests` |

### Retos, evidencias y jurado (004, 007, 008)

| ID | Caso | Resultado esperado | Automatizado en |
|---|---|---|---|
| CP-30 | Retarse a sí mismo | 422 `INVALID_TARGET` | `CoreFlowTests` |
| CP-31 | Aceptar el propio reto / dirigido a otro | 409 / 403 | `SocialFlowTests` |
| CP-32 | Aceptar sin saldo | 402 `INSUFFICIENT_POINTS`, saldo intacto | `SocialFlowTests` |
| CP-33 | Rechazar como retado | 200 `cancelled` + `rejected` | `SocialFlowTests` |
| CP-34 | Primera evidencia | reto en `voting`, 3 jurados sin retador ni ejecutor | `JuryFlowTests` |
| CP-35 | Enlace no HTTPS o dominio no permitido | 422 | `CoreFlowTests` |
| CP-36 | Cuarta evidencia | 409 `EVIDENCE_LIMIT_REACHED` | `CoreFlowTests` |
| CP-37 | Voto de no jurado / voto repetido | 403 `NOT_JUROR` / 409 `ALREADY_VOTED` | `JuryFlowTests` |
| CP-38 | 2 de 3 legítimo | `fulfilled`, fianza devuelta, compromiso creado | `JuryFlowTests` |
| CP-39 | Trampa | `cheating`, fianza al retador, sin compromiso | `JuryFlowTests` |
| CP-40 | Empate | `needs_admin`; solo admin resuelve | `JuryFlowTests` |
| CP-41 | Compromiso: pedir y confirmar | `completion_requested` → `completed`; repetir 409 | `JuryFlowTests` |
| CP-42 | Avisos: leer uno ajeno | 404; `read-all` deja 0 sin leer | `JuryFlowTests` |

### Seguridad transversal

| ID | Caso | Resultado esperado | Automatizado en |
|---|---|---|---|
| CP-50 | Cada ruta protegida sin token | 401 sin stack trace | `AuthorizationTests` (30+ rutas) |
| CP-51 | Token malformado | 401 | `AuthorizationTests` |
| CP-52 | Cabeceras de seguridad | `X-Content-Type-Options`, `X-Frame-Options`, etc. | `SecurityHeadersTests` |
| CP-53 | Imagen sin secretos locales | `.dockerignore` + Trivy secret scan | Pipeline |

### Infraestructura (001)

| ID | Caso | Resultado esperado | Dónde |
|---|---|---|---|
| CP-60 | `/health` tras desplegar | 200 `Healthy` por HTTPS | job `smoke` |
| CP-61 | `infra.yml` → `plan` | Sin errores | Actions (manual) |
| CP-62 | `infra.yml` → `destroy` sin `DESTRUIR` | Falla el guard, no borra nada | Actions (manual) |
| CP-63 | `mongo-init.js` dos veces | Sin errores ni duplicados | mongosh |

## 3. Cómo correr

```bash
dotnet test Pactogg.sln -c Release           # todas las suites
dotnet test IntegrationTest -c Release        # solo integración
```

En Visual Studio: **Prueba → Explorador de pruebas → Ejecutar todas**. Con Docker Desktop abierto también corre `DatabaseTest`.

## 4. Límites

Las pruebas de integración usan repositorios en memoria: no prueban transacciones reales de MongoDB, Redis, S3, Rekognition ni Comprehend; eso se verifica con el smoke y pruebas manuales contra AWS. Sin DAST ni pruebas de carga reales por ahora.
