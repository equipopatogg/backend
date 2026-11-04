# Constitución SDD — fase Backend PactoGG

Versión 3, 2026-11-03. Fase vigente: **corte vertical de evaluación implementado y verificado localmente; despliegue AWS y aceptación externa pendientes**.

## Alcance

Están autorizados el host ASP.NET Core .NET 8, MongoDB Atlas, Redis, JWT, moderación AWS/local, S3, Docker, Terraform, pipeline CI/CD y las pruebas de los siete endpoints de evaluación. No se amplía el stack con ORM, bases relacionales, microservicios, Kubernetes, GraphQL, gRPC, WebSocket, pagos ni video.

## Método

Spec por endpoint → plan → tareas → implementación → pruebas/evidencia → checklist. Cada expediente conserva `spec.md`, `plan.md`, `tasks.md` y `checklists/checklist.md`. Los IDs de requisitos, criterios y tareas deben citarse en pruebas, commits y `Memoria.md`.

## Estados

PASS, FAIL, ERROR, NO_EJECUTADO y NO_APLICA con motivo. Un build roto, una prueba ausente, un reporte corrupto o cero casos no son PASS. La verificación local no acredita AWS, TLS exterior, Atlas real ni aceptación humana.

## Límites

Los secretos viven fuera del repositorio: `appsettings.*.local.json`, `.env`, `*.tfvars` y SSM. No se registran contraseñas, tokens, correos completos, PII, URLs firmadas ni frases dictadas. Toda consulta filtra por `group_id` y verifica pertenencia o propiedad. La moderación ocurre antes de almacenar.

