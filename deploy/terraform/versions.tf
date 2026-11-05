terraform {
  required_version = ">= 1.10.0"

  # Estado remoto en S3 (creado por deploy/bootstrap) para crear y destruir desde el pipeline.
  # Configuración parcial: bucket/región van en backend.hcl (local) o en -backend-config (pipeline).
  backend "s3" {
    key          = "pactogg/prod/terraform.tfstate"
    encrypt      = true
    use_lockfile = true
  }

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.60"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = local.tags
  }
}
