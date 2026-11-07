# Tareas — 001 Infraestructura y DevSecOps

[Spec](spec.md) · [plan](plan.md).

- [x] 001-T1 — Terraform de red, 2 instancias, Caddy LB, TLS, S3/CloudFront, CloudWatch, SSM; 001-R1, 001-R3, 001-R4; criterio 001-AC1.
- [x] 001-T2 — Pipeline con build, suites, CodeQL, NuGet/Trivy, Terraform validate, Docker/ECR, SSM y smoke; 001-R6, 001-R8; criterio 001-AC3.
- [x] 001-T3 — Semgrep, Dockle, Gitleaks, Checkov, CycloneDX, SonarCloud, ZAP y compuerta `quality-gate`; 001-R6, 001-R7, 001-R8; criterios 001-AC3, 001-AC7.
- [x] 001-T4 — Estado remoto + `infra.yml` (plan/apply/destroy) + bootstrap; 001-R2; criterio 001-AC2.
- [x] 001-T5 — `database/mongo-init.js` con colecciones, validadores e índices; 001-R5; criterio 001-AC4.
- [x] 001-T6 — `.dockerignore` sin secretos locales; criterio 001-AC5.
- [x] 001-T7 — Budgets 50/80/100 % y CloudTrail en `deploy/bootstrap`; 001-R9 (código).
- [x] 001-T8 — `/health/ready` con chequeo de MongoDB; 001-R10; criterio 001-AC9 (2026-11-06).
- [ ] 001-T9 — Caddy a `/health/ready` junto con los parámetros SMTP y Firebase en `user_data` (reemplaza instancias; acción del equipo).
- [ ] 001-T10 — `terraform apply` en `deploy/bootstrap` (Budgets y CloudTrail); criterio 001-AC8 (acción de David).
- [ ] 001-T11 — Prueba manual de dos instancias: detener una y comprobar que la API responde; criterio 001-AC6.

## Definición de terminado

Pipeline en verde en un PR, `infra.yml plan` sin errores y Memoria actualizada.
