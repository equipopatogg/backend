# Bootstrap de una sola vez para crear y destruir la infraestructura DESDE EL PIPELINE.
#
# Crea lo que debe sobrevivir a un `terraform destroy` de deploy/terraform:
#   - Bucket S3 privado para el estado remoto (versionado, cifrado y con bloqueo nativo de S3).
#   - Proveedor OIDC de GitHub (se mueve aquí desde deploy/terraform).
#   - Rol `pactogg-github-terraform` que solo puede asumir el workflow infra.yml
#     en el GitHub Environment "infra" (con aprobación manual).
#
#   - AWS Budgets con alertas al 50, 80 y 100 % (CLARIFICACION #131/#132) y CloudTrail de eventos
#     de administración (siempre activo, CLARIFICACION #132 y seguridad de infraestructura).
#
# Costo: S3 con unos KB de estado y los logs de CloudTrail (capa gratuita); los 2 primeros budgets
# no tienen costo y el primer trail de eventos de administración es gratuito.
# Uso: bash scripts/bootstrap-terraform-pipeline.sh (lo corre todo, incluida la migración).

terraform {
  required_version = ">= 1.10.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.60"
    }
  }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project   = "pactogg"
      ManagedBy = "terraform-bootstrap"
    }
  }
}

variable "aws_region" {
  type    = string
  default = "us-east-1"
}

variable "github_repo_refs" {
  description = "Formatos del repo que GitHub pone en el claim sub (clásico y con IDs inmutables)."
  type        = list(string)
  default     = ["equipopatogg/backend", "equipopatogg@349079173/backend@1436429805"]
}

variable "github_environment" {
  description = "GitHub Environment que protege la creación/destrucción (con revisores obligatorios)."
  type        = string
  default     = "infra"
}

variable "budget_alert_email" {
  description = "Correo que recibe las alertas de gasto (cuenta del equipo equipopatogg)."
  type        = string

  validation {
    condition     = can(regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", var.budget_alert_email))
    error_message = "budget_alert_email debe ser un correo válido."
  }
}

variable "net_cost_limit_usd" {
  description = "Límite mensual de gasto NETO (después de créditos). El objetivo es 0; cualquier cobro real alerta."
  type        = string
  default     = "1"
}

variable "gross_cost_limit_usd" {
  description = "Límite mensual de gasto BRUTO (antes de créditos) para vigilar el consumo de créditos."
  type        = string
  default     = "30"
}

data "aws_caller_identity" "current" {}

data "aws_partition" "current" {}

locals {
  state_bucket = "pactogg-tfstate-${data.aws_caller_identity.current.account_id}"
}

resource "aws_s3_bucket" "state" {
  # checkov:skip=CKV_AWS_144: Replicación entre regiones duplica costo; el estado y los logs no son críticos para disponibilidad (costo cero, CLAR #131).
  # checkov:skip=CKV_AWS_145: SSE-S3 (AES256) es gratuito; una CMK de KMS cuesta 1 USD/mes.
  # checkov:skip=CKV_AWS_18: Los logs de acceso S3 requieren otro bucket; CloudTrail ya registra la administración.
  # checkov:skip=CKV2_AWS_62: No hay consumidores de eventos del bucket.
  bucket = local.state_bucket

  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_s3_bucket_versioning" "state" {
  bucket = aws_s3_bucket.state.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "state" {
  bucket = aws_s3_bucket.state.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_public_access_block" "state" {
  bucket                  = aws_s3_bucket.state.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_lifecycle_configuration" "state" {
  bucket = aws_s3_bucket.state.id

  rule {
    id     = "expire-old-state-versions"
    status = "Enabled"

    filter {}

    noncurrent_version_expiration {
      noncurrent_days = 30
    }

    abort_incomplete_multipart_upload {
      days_after_initiation = 1
    }
  }
}

resource "aws_s3_bucket_policy" "state_tls_only" {
  bucket = aws_s3_bucket.state.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Sid       = "DenyInsecureTransport"
      Effect    = "Deny"
      Principal = "*"
      Action    = "s3:*"
      Resource  = [aws_s3_bucket.state.arn, "${aws_s3_bucket.state.arn}/*"]
      Condition = { Bool = { "aws:SecureTransport" = "false" } }
    }]
  })
}

resource "aws_iam_openid_connect_provider" "github" {
  url             = "https://token.actions.githubusercontent.com"
  client_id_list  = ["sts.amazonaws.com"]
  thumbprint_list = ["6938fd4d98bab03faadb97b34396831e3780aea1"]

  tags = { Name = "pactogg-github-oidc" }
}

resource "aws_iam_role" "terraform" {
  name                 = "pactogg-github-terraform"
  max_session_duration = 3600

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Federated = aws_iam_openid_connect_provider.github.arn }
      Action    = "sts:AssumeRoleWithWebIdentity"
      Condition = {
        StringEquals = {
          "token.actions.githubusercontent.com:aud" = "sts.amazonaws.com"
        }
        # Solo jobs que usan el Environment "infra" (aprobación manual en GitHub).
        StringLike = {
          "token.actions.githubusercontent.com:sub" = [for repo in var.github_repo_refs : "repo:${repo}:environment:${var.github_environment}"]
        }
      }
    }]
  })
}

# Terraform crea VPC, EC2, IAM, S3, CloudFront, ECR, SSM y CloudWatch: necesita permisos amplios.
# El riesgo se acota con la confianza OIDC (solo el Environment "infra" con revisores) y el SCP de la organización.
resource "aws_iam_role_policy_attachment" "terraform_admin" {
  role       = aws_iam_role.terraform.name
  policy_arn = "arn:${data.aws_partition.current.partition}:iam::aws:policy/AdministratorAccess"
}

output "state_bucket" {
  value = aws_s3_bucket.state.bucket
}

output "terraform_role_arn" {
  value = aws_iam_role.terraform.arn
}

output "oidc_provider_arn" {
  value = aws_iam_openid_connect_provider.github.arn
}

# ---------------------------------------------------------------------------
# AWS Budgets: alertas al 50, 80 y 100 % (real) y al 100 % pronosticado.
# ---------------------------------------------------------------------------

locals {
  budget_thresholds = [50, 80, 100]
}

resource "aws_budgets_budget" "net_cost" {
  name         = "pactogg-costo-neto"
  budget_type  = "COST"
  limit_amount = var.net_cost_limit_usd
  limit_unit   = "USD"
  time_unit    = "MONTHLY"

  # Neto: con créditos y reembolsos descontados. Es lo que de verdad se pagaría.
  cost_types {
    include_credit = true
    include_refund = true
  }

  dynamic "notification" {
    for_each = local.budget_thresholds
    content {
      comparison_operator        = "GREATER_THAN"
      threshold                  = notification.value
      threshold_type             = "PERCENTAGE"
      notification_type          = "ACTUAL"
      subscriber_email_addresses = [var.budget_alert_email]
    }
  }

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 100
    threshold_type             = "PERCENTAGE"
    notification_type          = "FORECASTED"
    subscriber_email_addresses = [var.budget_alert_email]
  }
}

resource "aws_budgets_budget" "gross_cost" {
  name         = "pactogg-consumo-creditos"
  budget_type  = "COST"
  limit_amount = var.gross_cost_limit_usd
  limit_unit   = "USD"
  time_unit    = "MONTHLY"

  # Bruto: sin descontar créditos, para saber cuánto crédito se está gastando.
  cost_types {
    include_credit = false
    include_refund = false
  }

  dynamic "notification" {
    for_each = local.budget_thresholds
    content {
      comparison_operator        = "GREATER_THAN"
      threshold                  = notification.value
      threshold_type             = "PERCENTAGE"
      notification_type          = "ACTUAL"
      subscriber_email_addresses = [var.budget_alert_email]
    }
  }
}

# ---------------------------------------------------------------------------
# CloudTrail: eventos de administración de todas las regiones, siempre activo.
# ---------------------------------------------------------------------------

resource "aws_s3_bucket" "cloudtrail" {
  # checkov:skip=CKV_AWS_144: Replicación entre regiones duplica costo; el estado y los logs no son críticos para disponibilidad (costo cero, CLAR #131).
  # checkov:skip=CKV_AWS_145: SSE-S3 (AES256) es gratuito; una CMK de KMS cuesta 1 USD/mes.
  # checkov:skip=CKV_AWS_18: Los logs de acceso S3 requieren otro bucket; CloudTrail ya registra la administración.
  # checkov:skip=CKV2_AWS_62: No hay consumidores de eventos del bucket.
  bucket = "pactogg-cloudtrail-${data.aws_caller_identity.current.account_id}"

  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_s3_bucket_public_access_block" "cloudtrail" {
  bucket                  = aws_s3_bucket.cloudtrail.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_versioning" "cloudtrail" {
  bucket = aws_s3_bucket.cloudtrail.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "cloudtrail" {
  bucket = aws_s3_bucket.cloudtrail.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "cloudtrail" {
  bucket = aws_s3_bucket.cloudtrail.id

  rule {
    id     = "expirar-logs"
    status = "Enabled"

    filter {}

    expiration {
      days = 90
    }

    noncurrent_version_expiration {
      noncurrent_days = 7
    }

    abort_incomplete_multipart_upload {
      days_after_initiation = 1
    }
  }
}

data "aws_iam_policy_document" "cloudtrail_bucket" {
  statement {
    sid       = "AWSCloudTrailAclCheck"
    actions   = ["s3:GetBucketAcl"]
    resources = [aws_s3_bucket.cloudtrail.arn]

    principals {
      type        = "Service"
      identifiers = ["cloudtrail.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "aws:SourceArn"
      values   = ["arn:${data.aws_partition.current.partition}:cloudtrail:${var.aws_region}:${data.aws_caller_identity.current.account_id}:trail/pactogg-management"]
    }
  }

  statement {
    sid       = "AWSCloudTrailWrite"
    actions   = ["s3:PutObject"]
    resources = ["${aws_s3_bucket.cloudtrail.arn}/AWSLogs/${data.aws_caller_identity.current.account_id}/*"]

    principals {
      type        = "Service"
      identifiers = ["cloudtrail.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "s3:x-amz-acl"
      values   = ["bucket-owner-full-control"]
    }

    condition {
      test     = "StringEquals"
      variable = "aws:SourceArn"
      values   = ["arn:${data.aws_partition.current.partition}:cloudtrail:${var.aws_region}:${data.aws_caller_identity.current.account_id}:trail/pactogg-management"]
    }
  }

  statement {
    sid     = "DenyInsecureTransport"
    effect  = "Deny"
    actions = ["s3:*"]
    resources = [
      aws_s3_bucket.cloudtrail.arn,
      "${aws_s3_bucket.cloudtrail.arn}/*"
    ]

    principals {
      type        = "*"
      identifiers = ["*"]
    }

    condition {
      test     = "Bool"
      variable = "aws:SecureTransport"
      values   = ["false"]
    }
  }
}

resource "aws_s3_bucket_policy" "cloudtrail" {
  bucket = aws_s3_bucket.cloudtrail.id
  policy = data.aws_iam_policy_document.cloudtrail_bucket.json

  depends_on = [aws_s3_bucket_public_access_block.cloudtrail]
}

# La cuenta del proyecto está dentro de una organización de AWS cuya política de control (SCP) niega
# cloudtrail:CreateTrail (AccessDenied al aplicar el 2026-11-11, F2-D69). Por eso el trail queda apagado por defecto.
# El historial de eventos de CloudTrail (90 días de eventos de administración) sigue disponible gratis en la
# consola sin crear un trail. Si la organización quita esa restricción: TF_VAR_enable_cloudtrail=true terraform apply.
variable "enable_cloudtrail" {
  description = "Crear el trail pactogg-management (la SCP de la organización lo bloquea hoy)."
  type        = bool
  default     = false
}

resource "aws_cloudtrail" "management" {
  count = var.enable_cloudtrail ? 1 : 0

  # checkov:skip=CKV_AWS_35: Cifrado SSE-S3 sin CMK para no pagar KMS (costo cero); la validación de integridad está activa.
  # checkov:skip=CKV_AWS_252: Sin SNS; las alertas de gasto van por AWS Budgets.
  # checkov:skip=CKV2_AWS_10: CloudWatch Logs para el trail genera ingesta con costo; los eventos quedan en S3 90 días.
  name                          = "pactogg-management"
  s3_bucket_name                = aws_s3_bucket.cloudtrail.id
  include_global_service_events = true
  is_multi_region_trail         = true
  enable_log_file_validation    = true

  # Solo eventos de administración (gratis en el primer trail). Sin eventos de datos para no generar costo.
  event_selector {
    read_write_type           = "All"
    include_management_events = true
  }

  depends_on = [aws_s3_bucket_policy.cloudtrail]
}

output "cloudtrail_bucket" {
  value = aws_s3_bucket.cloudtrail.id
}
