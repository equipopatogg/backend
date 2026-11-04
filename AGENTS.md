# AGENTS.md — Reglas para agentes de IA en PactoGG

Backend ASP.NET Core **.NET 8** con MongoDB Atlas, Redis, JWT y AWS. Proyecto evaluado el **3 y 4 de noviembre de 2026**. Este archivo es de lectura obligatoria antes de tocar código.

## 1. Orden de lectura

1. `constitution.md` — norma superior y no negociable.
2. `Memoria.md` — estado actual, decisiones y pendientes.
3. `specs/open-questions.md` — dudas abiertas; no las resuelvas solo.
4. `specs/README.md` — índice de expedientes; elige el `spec.md` del endpoint o de la base.
5. `specs/<ID>/spec.md`, `plan.md`, `tasks.md`, `checklists/checklist.md` — fuente de la tarea.
6. `.opencode/skills/<skill>/SKILL.md` — ayuda de lectura si la tarea coincide.
7. `api/openapi.yaml` — contrato de la API.
8. `.specify/templates/spec/` — plantillas solo para crear un expediente nuevo.
9. Documentos fuente en `C:\Users\david\Downloads\` solo si necesitas contexto adicional.

Si dos fuentes se contradicen, manda la decisión del Product Owner registrada en las clarificaciones y en `constitution.md`. Registra la contradicción en `specs/open-questions.md`.

## 2. Reglas inquebrantables

- No inventes requisitos ni campos. Lo indefinido va a `specs/open-questions.md`.
- No proceses dinero: los puntos son virtuales y sin valor monetario.
- No subas videos: las evidencias son foto o enlace.
- Modera antes de almacenar: foto con Rekognition, texto con Comprehend.
- Toda consulta filtra por `group_id` y verifica pertenencia/propiedad (anti-IDOR).
- Nunca escribas secretos, cadenas de conexión, tokens ni claves en el repositorio.
- Nunca registres en logs contraseñas, tokens, `Authorization`, correos completos, PII, URLs firmadas ni frases dictadas.
- No agregues dependencias sin justificarlas en `plan.md` y sin revisar licencia/vulnerabilidades.
- No cambies comportamiento sin cambiar primero la especificación.
- Escribe pruebas antes o junto al código. No dejes pruebas rojas.
- Cita IDs (`BE-xx`, `USR-xx`, `AUTH-xx`, `GRP-xx`, `CON-xx`, `EVI-xx`, `TOR-xx`, etc.) en specs, commits y pruebas.
- Actualiza `Memoria.md` al terminar cada fase o decisión relevante.

## 3. Mapa del repositorio

```
PactoGG/
├── Pactogg.sln
├── constitution.md
├── AGENTS.md
├── Memoria.md
├── PREPARACION_SDD.md            # entrada rápida a los expedientes
├── .opencode/                    # agentes y skills de lectura
├── .specify/                     # constitución de fase y plantillas SDD
├── Pactogg.Api/                  # API principal
│   ├── Program.cs
│   ├── Modules/
│   │   ├── Auth/                 # registro, login, refresh, logout, OAuth, cuenta
│   │   ├── Groups/               # grupos, miembros, invitaciones
│   │   ├── Contracts/            # contratos, estados, voz/IA, jurado, puntos
│   │   ├── Evidence/             # fotos, enlaces, moderación, S3
│   │   └── Tournaments/          # torneos, brackets, partidas
│   ├── Infrastructure/
│   │   ├── Mongo/                # repositorios, índices, transacciones
│   │   ├── Redis/                # caché, rate limit, tokens revocados
│   │   ├── Security/             # JWT, hashing, autorización
│   │   ├── Storage/              # S3/CloudFront
│   │   └── Moderation/           # Rekognition/Comprehend + adaptadores
│   └── Common/                   # middleware, errores, request_id, DTOs base
├── ContractTest/                 # Pact / OpenAPI
├── DatabaseTest/                 # Testcontainers.MongoDb
├── IntegrationTest/              # WebApplicationFactory + Mongo/Redis reales
├── MutationTest/                 # Stryker.NET
├── PerformanceTest/              # NBomber
├── SecurityTest/                 # IDOR, JWT, rate limit, SSRF, NoSQL
├── UnitTest/                     # reglas de negocio
├── specs/                        # un expediente por endpoint y por base
│   ├── README.md                 # índice
│   ├── validation.md             # resultados locales
│   ├── open-questions.md         # dudas abiertas
│   └── <ID>/                     # spec, plan, tasks y checklist
├── api/openapi.yaml              # contrato vinculante
├── docs/adr/                     # decisiones de arquitectura
├── docs/endpoints.md             # índice de endpoints
├── deploy/terraform/             # IaC AWS
├── .github/workflows/            # CI/CD DevSecOps
└── scripts/                      # semillas, despliegue, calidad
```

## 4. Comandos

```bash
# Restaurar y compilar
dotnet restore Pactogg.sln
dotnet build Pactogg.sln -c Release --no-restore

# Ejecutar API
dotnet run --project Pactogg.Api/Pactogg.Api.csproj

# Pruebas por proyecto
dotnet test UnitTest/UnitTest.csproj -c Release --no-build
dotnet test IntegrationTest/IntegrationTest.csproj -c Release --no-build
dotnet test SecurityTest/SecurityTest.csproj -c Release --no-build
dotnet test ContractTest/ContractTest.csproj -c Release --no-build
dotnet test DatabaseTest/DatabaseTest.csproj -c Release --no-build

# Mutación (opcional, umbral 60 %)
dotnet tool restore
dotnet stryker --config-file MutationTest/stryker-config.json

# Rendimiento
dotnet run --project PerformanceTest/PerformanceTest.csproj -c Release
```

Antes de cada entrega ejecuta, como mínimo: `dotnet build`, `UnitTest`, `IntegrationTest`, `SecurityTest` y `ContractTest`.

## 5. Flujo de trabajo SDD

1. Lee `specs/README.md` y el expediente del endpoint o de la base. Si no existe, créalo desde `.specify/templates/spec/` antes de codificar.
2. Escribe `plan.md` con diseño, archivos, dependencias y riesgos.
3. Divide en `tasks.md` pequeñas y verificables.
4. Implementa una tarea con su prueba.
5. Ejecuta pruebas y escaneos.
6. Actualiza `specs/validation.md` y `Memoria.md`.
7. Solicita revisión humana.

Cada endpoint de evaluación tiene su propio expediente `specs/<ID>/` con `spec.md`, `plan.md`, `tasks.md` y `checklists/checklist.md`. Las rutas alias se documentan en el expediente principal. Los perfiles y skills de `.opencode/` son ayudas de lectura, no una autoridad paralela.

## 6. Convenciones de código

- Nullable e implicit usings activos; `async/await` en todo I/O; `CancellationToken` en servicios y repositorios.
- Controladores delgados; lógica en servicios de aplicación y dominio.
- DTOs explícitos; nunca exponer documentos de MongoDB ni entidades internas.
- Validación con DataAnnotations/FluentValidation; rechazo de campos desconocidos.
- Respuestas con envoltura `data`/`meta`; errores con `code`, `message`, `request_id`.
- Nombres de rutas en plural y minúsculas; IDs `ObjectId` como string en JSON.
- Sin `Console.WriteLine`; usar `ILogger` estructurado.
- Sin comentarios obvios; XML docs solo en superficies públicas cuando aporten.
- Sin `catch` vacíos; errores de infraestructura se traducen a errores de API seguros.
- Sin state estático mutable; Redis para estado compartido.
- Sin secretos ni valores por defecto inseguros en `appsettings.json`.

## 7. Seguridad por PR

Marca cada casilla antes de pedir revisión:

- [ ] Autorización verificada por grupo y por recurso.
- [ ] Validación de entrada y rechazo de claves NoSQL (`$`, `.`).
- [ ] Rate limit aplicado donde corresponde.
- [ ] Idempotencia en acciones sensibles.
- [ ] Sin secretos ni PII en código o logs.
- [ ] Errores sin stack traces ni detalles internos.
- [ ] Pruebas de abuso agregadas (IDOR, token reutilizado, límite, archivo inválido).
- [ ] OpenAPI actualizado.
- [ ] Sin hallazgos críticos/altos en SAST/SCA.

## 8. Endpoints de la evaluación (3 y 4 de noviembre)

1. `POST /api/v1/auth/register`
2. `POST /api/v1/auth/login`
3. `POST /api/v1/auth/logout`
4. `POST /api/v1/groups`
5. `POST /api/v1/contracts`
6. `POST /api/v1/contracts/{id}/evidence`
7. `GET /api/v1/tournaments`

Las rutas canónicas del CRUD para contratos y torneos están anidadas en `/groups/{gid}`. Mantén ambas formas y resuelve el grupo por pertenencia. Ver `specs/open-questions.md` Q-001 y Q-011.

## 9. Prohibiciones

- No usar Supabase, SQL Server, EF Core, PostgreSQL, DynamoDB ni DocumentDB.
- No usar microservicios, Kubernetes, GraphQL, gRPC, WebSocket, API Gateway, BFF ni CQRS.
- No implementar pagos, compras, ventas ni canjes.
- No almacenar video ni archivos subidos directamente sin moderación.
- No confiar en la salida de IA ni en enlaces externos.
- No desactivar HTTPS, CORS, rate limiting o validación para “hacer que funcione”.
- No hacer commit de `appsettings.*.local.json`, `.env`, `bin/`, `obj/`, reportes ni artefactos.

## 10. Criterio de terminado

Una tarea termina cuando compila, las pruebas pasan, los escaneos no tienen hallazgos críticos/altos, la documentación y OpenAPI están al día, `Memoria.md` se actualizó y otra persona revisó el cambio.
