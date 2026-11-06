# Tareas — 001 Infraestructura y DevSecOps

[Spec](spec.md) · [plan](plan.md).

- [x] 001-T1 — Terraform de red, 2 instancias, Caddy LB, TLS, S3/CloudFront, CloudWatch, SSM; criterio 001-AC1; evidencia `/health` público.
- [x] 001-T2 — Pipeline con build, 7 suites, CodeQL, NuGet/Trivy, Terraform validate, Docker/ECR, SSM y smoke; criterio 001-AC3.
- [x] 001-T3 — Semgrep (SAST), Dockle (imagen) y compuerta `quality-gate`; criterio 001-AC3; evidencia job en PR.
- [x] 001-T4 — Estado remoto + `infra.yml` (plan/apply/destroy) + bootstrap; criterio 001-AC2.
- [x] 001-T5 — `database/mongo-init.js` con colecciones, validadores e índices; criterio 001-AC4.
- [x] 001-T6 — `.dockerignore` sin secretos locales; criterio 001-AC5.
- [ ] 001-T7 — Ejecutar `scripts/bootstrap-terraform-pipeline.sh` y crear Environment `infra` (acción del equipo).

## Definición de terminado

Pipeline en verde en un PR, `infra.yml plan` sin errores y Memoria actualizada.
