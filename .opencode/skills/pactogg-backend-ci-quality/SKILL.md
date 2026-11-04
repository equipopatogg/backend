---
name: pactogg-backend-ci-quality
description: Revisa CI, Terraform, Docker, SAST/SCA y evidencia de PactoGG sin ejecutar servicios remotos.
---

# CI y calidad con evidencia identificable

Aplicar [AGENTS](../../../AGENTS.md), [constitución](../../../constitution.md) y [fase](../../../.specify/memory/constitution.md). Consultar [BE-01](../../../specs/BE-01-pipeline-ci/spec.md), [BE-03](../../../specs/BE-03-quality/spec.md) y [validación](../../../specs/validation.md).

- Revisar `.github/workflows/backend-ci-cd.yml`: build/test, SAST, SCA, Terraform validate, Docker/Trivy, ECR, ASG refresh y smoke.
- Distinguir SAST, SCA, SBOM y escaneo de imagen; un reporte no acredita los demás.
- Terraform validate no equivale a plan/apply. Sin credenciales AWS, dominio, Route53 ni Atlas real, el despliegue sigue NO_EJECUTADO.
- Docker local ausente no invalida los tests en memoria; Testcontainers se omite y debe declararse.
- Ausencia de informe, cero pruebas o error de herramienta nunca son PASS.

Salida: criterio BE01/BE03 afectado, decisión abierta y evidencia disponible. No escribir YAML ni activar CI por inferencia.

