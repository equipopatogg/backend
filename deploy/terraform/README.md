# PactoGG AWS — despliegue de costo cero

Requisito del docente: backend en AWS **con costo cero**, al menos **2 instancias**, **load balancer o similar**, **HTTPS/SSL**, **Redis distribuido**, **CDN** para imágenes, **observabilidad** y creación/eliminación con **Terraform**.

## Arquitectura

| Pieza | Implementación | Costo |
|---|---|---|
| 2 instancias | 2 × EC2 `t3.micro` en AZ distintas, IP privada fija | créditos |
| Load balancer | **Caddy** en la instancia 1: round-robin a las dos APIs con health checks (`/health`) | $0 |
| HTTPS | Let's Encrypt automático vía Caddy con `<ip-con-guiones>.sslip.io` (o `domain_name` propio) | $0 |
| Redis distribuido | `redis:7-alpine` en la instancia 1, compartido por ambas APIs (contraseña en SSM) | $0 |
| Imágenes / CDN | S3 privado + CloudFront con Origin Access Control | siempre gratis (1 TB/mes) |
| Observabilidad | CloudWatch Logs (driver `awslogs` de Docker) | siempre gratis (5 GB) |
| Secretos | SSM Parameter Store `SecureString` | $0 |
| Imagen | ECR | centavos |

Sin NAT Gateway, sin ALB y sin ElastiCache: eran ~$80/mes. Esta versión cuesta ~$25/mes (EC2 + 2 IPv4 públicas + disco), cubiertos por los créditos de la cuenta; la tarjeta no se toca mientras haya créditos. Apágalo con `destroy.sh` al terminar la evaluación.

Instancia 1 (`gateway`): Caddy (80/443) + API + Redis. Instancia 2 (`worker`): API; solo acepta tráfico 8080 desde la instancia 1. Sin SSH: administración por SSM Session Manager.

## Primer despliegue (WSL)

```bash
cd /mnt/g/PactoGG
bash scripts/deploy-first-time.sh
```

El script instala AWS CLI/Terraform si faltan, configura el perfil `pactogg`, comprueba permisos, guarda la URI de Atlas y la clave JWT en `deploy/terraform/.env.secrets` (ignorado), crea ECR, publica la imagen, muestra el plan, pide confirmación, aplica y espera a que `/health` responda 200 por HTTPS. Al final imprime las dos IP que hay que permitir en MongoDB Atlas.

## Redesplegar una imagen nueva

```bash
AWS_PROFILE=pactogg bash scripts/deploy-aws.sh
```

Ejecuta `/opt/pactogg/redeploy.sh` en ambas instancias mediante SSM Run Command (pull de `:latest` y reinicio del contenedor).

## GitHub Actions

- Secret `AWS_ROLE_ARN`: output `github_actions_role_arn`.
- Variables `AWS_REGION`, `ECR_REPOSITORY`, `INSTANCE_IDS` (output `instance_ids`, separados por espacio) y `PUBLIC_BASE_URL` (output `api_url`).

## Destruir

```bash
bash deploy/terraform/destroy.sh
```

Vacía el bucket de evidencias y ejecuta `terraform destroy`.

## Secretos

El estado de Terraform contiene la URI de Mongo, la clave JWT y la contraseña de Redis. No se versiona (`*.tfstate` en `.gitignore`).


## Crear y eliminar desde el pipeline

El estado vive en S3 (`pactogg-tfstate-<cuenta>`), creado por `deploy/bootstrap` junto con el proveedor OIDC de GitHub y el rol `pactogg-github-terraform`. Esos tres recursos sobreviven a un `destroy`.

1. Una sola vez, desde WSL: `bash scripts/bootstrap-terraform-pipeline.sh` (migra el estado local a S3 y configura los secretos si tienes `gh`).
2. En GitHub: Environment `infra` con revisores obligatorios y los secretos `AWS_TERRAFORM_ROLE_ARN`, `MONGO_CONNECTION_STRING`, `JWT_SIGNING_KEY`.
3. Actions → **infra** → Run workflow → `plan`, `apply` o `destroy` (escribiendo `DESTRUIR`).

En local, después de la migración: `terraform init -backend-config=backend.hcl`.

Al recrear, las Elastic IP cambian: agrega las nuevas IP a la lista de acceso de MongoDB Atlas y actualiza la URL de la API en la app.
