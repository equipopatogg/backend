# Plan — BE-01

[Spec](spec.md).

## Enfoque

Pipeline único con jobs dependientes, OIDC y evidencia por job. ECR como registro principal; DockerHub opcional.

## Archivos

| Ruta | Cambio |
|---|---|
| `.github/workflows/backend-ci-cd.yml` | Jobs build-test, sast, sca, terraform, docker, deploy, smoke |
| `deploy/terraform/` | Infraestructura y outputs |
| `scripts/deploy-aws.sh` | Redespliegue SSM |
| `scripts/deploy-first-time.sh` | Primer despliegue |
| `Dockerfile` | Imagen de ejecución |

## Dependencias

GitHub Actions, CodeQL, Trivy, AWS OIDC, ECR, SSM Run Command, Terraform 1.9.8. Sin claves AWS estáticas.

## Pruebas

Validación YAML/shell, Terraform validate, build/test local, SCA. Deploy y smoke solo con credenciales.

## Riesgos

Rol OIDC mal configurado, instancias sin imagen, health engañoso, secretos ausentes, Caddy como punto único de entrada.

## Orden

1. Build/test.
2. SAST/SCA/Terraform.
3. Docker/ECR.
4. Deploy/smoke.

## Cierre

Checklist, validación y `Memoria.md`.

