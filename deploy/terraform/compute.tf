# Dos instancias EC2 en AZ distintas (requisito: "al menos 2 instancias").
# Caddy en el gateway balancea round-robin entre ambas con health checks
# (requisito: "load balancer o similar") y obtiene TLS gratis de Let's Encrypt.
# IPs privadas fijas para romper la dependencia circular gateway <-> worker.

resource "aws_eip" "gateway" {
  domain = "vpc"
  tags   = { Name = "${local.name_prefix}-gateway" }
}

resource "aws_eip" "worker" {
  domain = "vpc"
  tags   = { Name = "${local.name_prefix}-worker" }
}

locals {
  gateway_private_ip = cidrhost(local.public_subnet_cidrs[0], 10)
  worker_private_ip  = cidrhost(local.public_subnet_cidrs[1], 10)

  # 3.15.22.10 -> 3-15-22-10.sslip.io (DNS público gratuito que resuelve a esa IP).
  public_host     = var.domain_name != "" ? var.domain_name : "${replace(aws_eip.gateway.public_ip, ".", "-")}.sslip.io"
  public_base_url = "https://${local.public_host}"

  common_user_data = {
    aws_region           = var.aws_region
    ssm_prefix           = local.ssm_prefix
    ecr_registry         = split("/", aws_ecr_repository.api.repository_url)[0]
    image_uri            = "${aws_ecr_repository.api.repository_url}:latest"
    log_group            = aws_cloudwatch_log_group.api.name
    jwt_issuer           = var.jwt_issuer
    jwt_audience         = var.jwt_audience
    jwt_access_minutes   = var.jwt_access_token_minutes
    jwt_refresh_days     = var.jwt_refresh_token_days
    privacy_version      = var.privacy_version
    s3_bucket_name       = aws_s3_bucket.evidence.bucket
    cdn_base_url         = "https://${aws_cloudfront_distribution.evidence.domain_name}"
    cors_allowed_origins = local.cors_allowed_origins
    gateway_private_ip   = local.gateway_private_ip
    worker_private_ip    = local.worker_private_ip
    public_host          = local.public_host
    acme_email           = var.acme_email
  }
}

resource "aws_instance" "gateway" {
  ami                    = data.aws_ami.ubuntu.id
  instance_type          = var.instance_type
  subnet_id              = aws_subnet.public[0].id
  private_ip             = local.gateway_private_ip
  vpc_security_group_ids = [aws_security_group.gateway.id]
  iam_instance_profile   = aws_iam_instance_profile.api.name

  # La AMI se resuelve con la imagen de Ubuntu más reciente; sin esto cada imagen nueva de Canonical
  # recrearía las 2 instancias en el siguiente plan. Para actualizar el sistema operativo a propósito,
  # usar terraform apply -replace=aws_instance.gateway (y worker).
  lifecycle {
    ignore_changes = [ami]
  }

  user_data_replace_on_change = true
  user_data = templatefile("${path.module}/user_data.sh.tftpl", merge(local.common_user_data, {
    role               = "gateway"
    expected_public_ip = aws_eip.gateway.public_ip
  }))

  metadata_options {
    http_endpoint               = "enabled"
    http_tokens                 = "required"
    http_put_response_hop_limit = 2
  }

  root_block_device {
    volume_size           = var.root_volume_size
    volume_type           = "gp3"
    encrypted             = true
    delete_on_termination = true
  }

  credit_specification {
    cpu_credits = "standard"
  }

  tags = { Name = "${local.name_prefix}-api-1-gateway", Role = "api" }

  # Las reglas de salida deben existir antes del primer arranque: sin ellas el
  # user_data no tiene internet y falla (incidente 2026-11-04).
  depends_on = [
    aws_ssm_parameter.mongo_connection_string,
    aws_ssm_parameter.jwt_signing_key,
    aws_ssm_parameter.redis_password,
    aws_ssm_parameter.redis_connection_string,
    aws_vpc_security_group_egress_rule.gateway_all,
    aws_vpc_security_group_egress_rule.worker_all,
    aws_vpc_security_group_ingress_rule.gateway_http,
    aws_vpc_security_group_ingress_rule.gateway_https,
    aws_vpc_security_group_ingress_rule.gateway_redis_from_worker,
    aws_vpc_security_group_ingress_rule.worker_api_from_gateway,
    aws_route_table_association.public,
  ]
}

resource "aws_instance" "worker" {
  ami                    = data.aws_ami.ubuntu.id
  instance_type          = var.instance_type
  subnet_id              = aws_subnet.public[1].id
  private_ip             = local.worker_private_ip
  vpc_security_group_ids = [aws_security_group.worker.id]
  iam_instance_profile   = aws_iam_instance_profile.api.name

  # La AMI se resuelve con la imagen de Ubuntu más reciente; sin esto cada imagen nueva de Canonical
  # recrearía las 2 instancias en el siguiente plan. Para actualizar el sistema operativo a propósito,
  # usar terraform apply -replace=aws_instance.gateway (y worker).
  lifecycle {
    ignore_changes = [ami]
  }

  user_data_replace_on_change = true
  user_data = templatefile("${path.module}/user_data.sh.tftpl", merge(local.common_user_data, {
    role               = "worker"
    expected_public_ip = aws_eip.worker.public_ip
  }))

  metadata_options {
    http_endpoint               = "enabled"
    http_tokens                 = "required"
    http_put_response_hop_limit = 2
  }

  root_block_device {
    volume_size           = var.root_volume_size
    volume_type           = "gp3"
    encrypted             = true
    delete_on_termination = true
  }

  credit_specification {
    cpu_credits = "standard"
  }

  tags = { Name = "${local.name_prefix}-api-2-worker", Role = "api" }

  # Las reglas de salida deben existir antes del primer arranque: sin ellas el
  # user_data no tiene internet y falla (incidente 2026-11-04).
  depends_on = [
    aws_ssm_parameter.mongo_connection_string,
    aws_ssm_parameter.jwt_signing_key,
    aws_ssm_parameter.redis_password,
    aws_ssm_parameter.redis_connection_string,
    aws_vpc_security_group_egress_rule.gateway_all,
    aws_vpc_security_group_egress_rule.worker_all,
    aws_vpc_security_group_ingress_rule.gateway_http,
    aws_vpc_security_group_ingress_rule.gateway_https,
    aws_vpc_security_group_ingress_rule.gateway_redis_from_worker,
    aws_vpc_security_group_ingress_rule.worker_api_from_gateway,
    aws_route_table_association.public,
  ]
}

resource "aws_eip_association" "gateway" {
  allocation_id = aws_eip.gateway.id
  instance_id   = aws_instance.gateway.id
}

resource "aws_eip_association" "worker" {
  allocation_id = aws_eip.worker.id
  instance_id   = aws_instance.worker.id
}
