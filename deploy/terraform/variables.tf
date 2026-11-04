variable "aws_region" {
  description = "Región AWS del despliegue."
  type        = string
  default     = "us-east-1"
}

variable "project_name" {
  description = "Prefijo de recursos."
  type        = string
  default     = "pactogg"
}

variable "environment" {
  description = "Entorno lógico del despliegue."
  type        = string
  default     = "prod"
}

variable "vpc_cidr" {
  description = "CIDR de la VPC."
  type        = string
  default     = "10.0.0.0/16"
}

variable "instance_type" {
  description = "Tipo de instancia EC2 de las dos instancias de la API."
  type        = string
  default     = "t3.micro"
}

variable "domain_name" {
  description = "Dominio propio que apunte (registro A) a la Elastic IP del gateway. Vacío: <ip-con-guiones>.sslip.io, gratis."
  type        = string
  default     = ""
}

variable "acme_email" {
  description = "Correo para avisos de Let's Encrypt."
  type        = string
  default     = "equipopatogg@gmail.com"
}

variable "root_volume_size" {
  description = "Disco raíz en GB (gp3)."
  type        = number
  default     = 12
}

variable "s3_bucket_name" {
  description = "Nombre globalmente único del bucket privado de evidencias."
  type        = string
}

variable "s3_force_destroy" {
  description = "Permite destruir el bucket aunque contenga objetos. Úsalo solo en entornos controlados."
  type        = bool
  default     = true
}

variable "mongo_connection_string" {
  description = "Cadena TLS de MongoDB Atlas con usuario de mínimo privilegio."
  type        = string
  sensitive   = true
}

variable "jwt_signing_key" {
  description = "Clave HMAC de JWT de al menos 32 bytes."
  type        = string
  sensitive   = true

  validation {
    condition     = length(var.jwt_signing_key) >= 32
    error_message = "jwt_signing_key debe tener al menos 32 caracteres."
  }
}

variable "jwt_issuer" {
  description = "Issuer de JWT."
  type        = string
  default     = "pactogg"
}

variable "jwt_audience" {
  description = "Audience de JWT."
  type        = string
  default     = "pactogg-app"
}

variable "jwt_access_token_minutes" {
  description = "Duración del access token en minutos."
  type        = number
  default     = 15
}

variable "jwt_refresh_token_days" {
  description = "Duración del refresh token en días."
  type        = number
  default     = 30
}

variable "privacy_version" {
  description = "Versión vigente del consentimiento de privacidad."
  type        = string
  default     = "2026-10-01"
}

variable "cors_allowed_origins" {
  description = "Orígenes CORS permitidos (landing web). Vacío: sin CORS."
  type        = list(string)
  default     = []
}

variable "ssm_parameter_prefix" {
  description = "Prefijo SSM para secretos de la API. Debe empezar por /."
  type        = string
  default     = "/pactogg/prod"
}

variable "ecr_repository_name" {
  description = "Nombre del repositorio ECR de la API."
  type        = string
  default     = "pactogg-api"
}

variable "github_repo" {
  description = "Repositorio GitHub en formato owner/repo para OIDC."
  type        = string
  default     = "equipopatogg/backend"
}

variable "github_branch" {
  description = "Rama autorizada para desplegar."
  type        = string
  default     = "main"
}

variable "github_environment" {
  description = "GitHub Environment autorizado para desplegar."
  type        = string
  default     = "production"
}

variable "create_github_oidc_provider" {
  description = "Crea el proveedor OIDC de GitHub. Desactívalo si ya existe en la cuenta."
  type        = bool
  default     = true
}

variable "log_retention_days" {
  description = "Retención de logs de CloudWatch."
  type        = number
  default     = 30
}

variable "tags" {
  description = "Tags adicionales para todos los recursos."
  type        = map(string)
  default     = {}
}
