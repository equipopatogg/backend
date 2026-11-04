resource "random_password" "redis_auth" {
  length  = 32
  special = false
}

resource "aws_ssm_parameter" "mongo_connection_string" {
  name  = "${local.ssm_prefix}/MongoDb__ConnectionString"
  type  = "SecureString"
  value = var.mongo_connection_string

  tags = { Name = "${local.name_prefix}-mongo-connection" }
}

resource "aws_ssm_parameter" "jwt_signing_key" {
  name  = "${local.ssm_prefix}/Jwt__SigningKey"
  type  = "SecureString"
  value = var.jwt_signing_key

  tags = { Name = "${local.name_prefix}-jwt-signing-key" }
}

resource "aws_ssm_parameter" "redis_password" {
  name  = "${local.ssm_prefix}/Redis__Password"
  type  = "SecureString"
  value = random_password.redis_auth.result

  tags = { Name = "${local.name_prefix}-redis-password" }
}

# Redis corre en el gateway; ambas instancias lo usan como caché distribuida.
resource "aws_ssm_parameter" "redis_connection_string" {
  name  = "${local.ssm_prefix}/Redis__ConnectionString"
  type  = "SecureString"
  value = "${cidrhost(local.public_subnet_cidrs[0], 10)}:6379,password=${random_password.redis_auth.result},abortConnect=false"

  tags = { Name = "${local.name_prefix}-redis-connection" }
}
