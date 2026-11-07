# Validación — 001 Infraestructura y DevSecOps

- [x] Costo cero respetado (sin ALB/NAT/ElastiCache/ASG).
- [x] ≥ 2 instancias detrás de balanceador (Caddy) con health checks.
- [x] HTTPS válido (Let's Encrypt) y CDN (CloudFront) para imágenes.
- [x] Observabilidad: CloudWatch Logs (retención 30 días) y `/health/ready` con la base de datos.
- [x] Redis para caché y límites de peticiones, con respaldo local si se cae.
- [x] Terraform crea y elimina (local y desde `infra.yml`).
- [x] Pipeline: SAST (CodeQL, Semgrep), SCA/SBOM, secretos (Gitleaks), IaC (Checkov), imagen (Trivy, Dockle), DAST (ZAP) y compuerta.
- [x] Script de la BD en el repositorio.
- [x] Sin secretos en Git ni en la imagen.
- [x] Spec completa según CLARIFICACION #138: fuera de alcance, EARS, criterios por requisito, cero preguntas abiertas, verificación contra la constitución.
- [x] Aprobación del Product Owner (2026-11-06).
- [ ] Revisión técnica (Kevin) y comprobabilidad (José).
- [ ] Budgets/CloudTrail aplicados y prueba manual de dos instancias (001-T10, 001-T11).
