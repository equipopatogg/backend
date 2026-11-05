# Validación — 001 Infraestructura y DevSecOps

- [x] Costo cero respetado (sin ALB/NAT/ElastiCache/ASG).
- [x] ≥ 2 instancias detrás de balanceador (Caddy) con health checks.
- [x] HTTPS válido (Let's Encrypt) y CDN (CloudFront) para imágenes.
- [x] Observabilidad: CloudWatch Logs (retención 30 días).
- [x] Redis para caché y límites de peticiones.
- [x] Terraform crea y elimina (local y desde `infra.yml`).
- [x] Pipeline: SAST (CodeQL, Semgrep), SCA/secretos (Trivy, NuGet), imagen (Trivy, Dockle), compuerta.
- [x] Script de la BD en el repositorio.
- [x] Sin secretos en Git ni en la imagen.
- [x] Coherente con la constitución (§7 seguridad, §9 infraestructura).
