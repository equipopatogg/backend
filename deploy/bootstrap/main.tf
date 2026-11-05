# Bootstrap de una sola vez para crear y destruir la infraestructura DESDE EL PIPELINE.
#
# Crea lo que debe sobrevivir a un `terraform destroy` de deploy/terraform:
#   - Bucket S3 privado para el estado remoto (versionado, cifrado y con bloqueo nativo de S3).
#   - Proveedor OIDC de GitHub (se mueve aquí desde deploy/terraform).
#   - Rol `pactogg-github-terraform` que solo puede asumir el workflow infra.yml
#     en el GitHub Environment "infra" (con aprobación manual).
#
# Costo: S3 con unos KB de estado; dentro de la capa gratuita.
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

data "aws_caller_identity" "current" {}

data "aws_partition" "current" {}

locals {
  state_bucket = "pactogg-tfstate-${data.aws_caller_identity.current.account_id}"
}

resource "aws_s3_bucket" "state" {
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
