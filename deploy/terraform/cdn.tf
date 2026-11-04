# CDN para imágenes (requisito: "subir en el CDN de la nube").
# CloudFront lee del bucket privado mediante Origin Access Control; el bucket
# sigue sin acceso público directo. Siempre gratis hasta 1 TB/mes de salida.

resource "aws_cloudfront_origin_access_control" "evidence" {
  name                              = "${local.name_prefix}-evidence-oac"
  description                       = "Acceso de CloudFront al bucket de evidencias."
  origin_access_control_origin_type = "s3"
  signing_behavior                  = "always"
  signing_protocol                  = "sigv4"
}

resource "aws_cloudfront_distribution" "evidence" {
  enabled         = true
  comment         = "${local.name_prefix} imagenes"
  price_class     = "PriceClass_100"
  is_ipv6_enabled = true
  http_version    = "http2and3"

  origin {
    origin_id                = "evidence-s3"
    domain_name              = aws_s3_bucket.evidence.bucket_regional_domain_name
    origin_access_control_id = aws_cloudfront_origin_access_control.evidence.id
  }

  default_cache_behavior {
    target_origin_id           = "evidence-s3"
    viewer_protocol_policy     = "redirect-to-https"
    allowed_methods            = ["GET", "HEAD"]
    cached_methods             = ["GET", "HEAD"]
    compress                   = true
    cache_policy_id            = "658327ea-f89d-4fab-a63d-7e88639e58f6" # Managed-CachingOptimized
    response_headers_policy_id = "67f7725c-6f97-4210-82d7-5512b31e9d03" # Managed-SecurityHeadersPolicy
  }

  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  viewer_certificate {
    cloudfront_default_certificate = true
    minimum_protocol_version       = "TLSv1.2_2021"
  }

  tags = { Name = "${local.name_prefix}-cdn" }
}
