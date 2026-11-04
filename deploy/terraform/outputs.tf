output "api_url" {
  description = "URL HTTPS pública de la API (Caddy + Let's Encrypt)."
  value       = local.public_base_url
}

output "health_check_url" {
  description = "URL de smoke test."
  value       = "${local.public_base_url}/health"
}

output "gateway_public_ip" {
  description = "Elastic IP del gateway (instancia 1). Permitirla en MongoDB Atlas."
  value       = aws_eip.gateway.public_ip
}

output "worker_public_ip" {
  description = "Elastic IP de salida de la instancia 2. Permitirla en MongoDB Atlas."
  value       = aws_eip.worker.public_ip
}

output "instance_ids" {
  description = "IDs de las dos instancias de la API."
  value       = [aws_instance.gateway.id, aws_instance.worker.id]
}

output "ecr_repository_url" {
  description = "Repositorio ECR para publicar imágenes."
  value       = aws_ecr_repository.api.repository_url
}

output "cdn_base_url" {
  description = "CDN CloudFront para las imágenes del bucket de evidencias."
  value       = "https://${aws_cloudfront_distribution.evidence.domain_name}"
}

output "github_actions_role_arn" {
  description = "ARN del rol OIDC para GitHub Actions."
  value       = aws_iam_role.github_actions.arn
}

output "evidence_bucket" {
  description = "Bucket privado de evidencias."
  value       = aws_s3_bucket.evidence.bucket
}

output "ssm_parameter_prefix" {
  description = "Prefijo de parámetros SSM de la API."
  value       = local.ssm_prefix
}

output "log_group" {
  description = "Grupo de CloudWatch Logs (observabilidad)."
  value       = aws_cloudwatch_log_group.api.name
}
