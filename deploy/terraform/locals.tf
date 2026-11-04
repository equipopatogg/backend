locals {
  name_prefix = "${var.project_name}-${var.environment}"
  ssm_prefix  = trimsuffix(var.ssm_parameter_prefix, "/")

  tags = merge({
    Project     = var.project_name
    Environment = var.environment
    ManagedBy   = "terraform"
  }, var.tags)

  azs = slice(data.aws_availability_zones.available.names, 0, 2)

  public_subnet_cidrs = [for index in range(2) : cidrsubnet(var.vpc_cidr, 8, index)]

  # Vacío por defecto: la app móvil no usa CORS. Define cors_allowed_origins
  # si un frontend web (landing) consume la API desde el navegador.
  cors_allowed_origins = var.cors_allowed_origins

  github_oidc_arn = var.create_github_oidc_provider ? aws_iam_openid_connect_provider.github[0].arn : data.aws_iam_openid_connect_provider.github[0].arn
}
