# gateway: Caddy (80/443 públicos) + API + Redis.
# worker:  solo API; recibe tráfico únicamente del gateway.
# Sin SSH: la administración es por SSM Session Manager.

resource "aws_security_group" "gateway" {
  name        = "${local.name_prefix}-gateway"
  description = "Caddy publico (HTTP/HTTPS), API y Redis compartido."
  vpc_id      = aws_vpc.main.id

  tags = { Name = "${local.name_prefix}-gateway" }
}

resource "aws_security_group" "worker" {
  name        = "${local.name_prefix}-worker"
  description = "Segunda instancia de la API; solo accesible desde el gateway."
  vpc_id      = aws_vpc.main.id

  tags = { Name = "${local.name_prefix}-worker" }
}

resource "aws_vpc_security_group_ingress_rule" "gateway_http" {
  security_group_id = aws_security_group.gateway.id
  description       = "HTTP para el reto ACME de Lets Encrypt y redireccion a HTTPS."
  cidr_ipv4         = "0.0.0.0/0"
  from_port         = 80
  to_port           = 80
  ip_protocol       = "tcp"
}

resource "aws_vpc_security_group_ingress_rule" "gateway_https" {
  security_group_id = aws_security_group.gateway.id
  description       = "HTTPS publico."
  cidr_ipv4         = "0.0.0.0/0"
  from_port         = 443
  to_port           = 443
  ip_protocol       = "tcp"
}

resource "aws_vpc_security_group_ingress_rule" "gateway_redis_from_worker" {
  security_group_id            = aws_security_group.gateway.id
  description                  = "Redis compartido para la segunda instancia."
  referenced_security_group_id = aws_security_group.worker.id
  from_port                    = 6379
  to_port                      = 6379
  ip_protocol                  = "tcp"
}

resource "aws_vpc_security_group_ingress_rule" "worker_api_from_gateway" {
  security_group_id            = aws_security_group.worker.id
  description                  = "Solo Caddy del gateway alcanza la API."
  referenced_security_group_id = aws_security_group.gateway.id
  from_port                    = 8080
  to_port                      = 8080
  ip_protocol                  = "tcp"
}

resource "aws_vpc_security_group_egress_rule" "gateway_all" {
  security_group_id = aws_security_group.gateway.id
  description       = "Salida: ECR, SSM, S3, Atlas, Lets Encrypt, APT y la API del worker."
  cidr_ipv4         = "0.0.0.0/0"
  ip_protocol       = "-1"
}

resource "aws_vpc_security_group_egress_rule" "worker_all" {
  security_group_id = aws_security_group.worker.id
  description       = "Salida: ECR, SSM, S3, Atlas, APT y Redis del gateway."
  cidr_ipv4         = "0.0.0.0/0"
  ip_protocol       = "-1"
}
