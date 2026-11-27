# Riesgos aceptados — Backend e infraestructura AWS

Fecha: 2026-11-27. Responsable: David Montoya (Backend). Revisión: antes de cada versión.

Cada riesgo tiene su motivo, el control que lo reduce y qué haría falta para cerrarlo. Las excepciones de Checkov están en [`.checkov.yaml`](../../.checkov.yaml).

| ID | Riesgo | Por qué se acepta | Control que lo reduce | Para cerrarlo |
|---|---|---|---|---|
| RA-01 | CloudFront de evidencias con `minimum_protocol_version = TLSv1` (CKV_AWS_174, CKV2_AWS_42) | Con el certificado por defecto `*.cloudfront.net` AWS **solo** permite `TLSv1`; TLS 1.2 mínimo exige un certificado propio de ACM y por lo tanto un dominio comprado | Los clientes modernos (Android 7+ de la app) negocian TLS 1.2/1.3; la distribución solo sirve imágenes ya moderadas y con URL firmada por el backend; cabeceras de seguridad (`Managed-SecurityHeadersPolicy`) y `redirect-to-https`. La API va por Caddy con TLS 1.2+ (Let's Encrypt) | Comprar dominio, certificado ACM en us-east-1 y `TLSv1.2_2021` |
| RA-02 | CloudTrail apagado (`enable_cloudtrail = false`) | La cuenta pertenece a una organización de AWS cuya SCP niega `cloudtrail:CreateTrail` (F2-D69). El recurso existe en `cloudtrail.tf` y se activa con la variable | El historial de eventos de CloudTrail (90 días, siempre activo y gratis en toda cuenta) sigue registrando la administración; alarmas de CloudWatch y AWS Budgets avisan por correo | Quitar la SCP o usar una cuenta fuera de la organización y poner `enable_cloudtrail = true` |
| RA-03 | Sin WAF en CloudFront (CKV_AWS_68, CKV2_AWS_47) | WAF cuesta ~5 USD/mes más reglas; el requisito es costo cero | Límite de peticiones por usuario e IP en la API, validación de entradas, CloudFront solo lectura | Activarlo si los créditos lo permiten |
| RA-04 | Cifrado con llaves administradas por AWS, no CMK propia (CKV_AWS_337, 136, 158, 35, 145) | Cada CMK cuesta 1 USD/mes | Todo está cifrado en reposo (SSE-S3, `aws/ssm`, AES256 en ECR, CloudWatch por defecto) | Crear CMK y referenciarla |
| RA-05 | Sin VPC Flow Logs, logs de acceso de S3/CloudFront ni CloudTrail→CloudWatch (CKV2_AWS_11, CKV_AWS_18, 86, CKV2_AWS_10, 252) | La ingesta y el almacenamiento tienen costo | Logs de la aplicación en CloudWatch (14 días) con alarmas de errores 5xx; grupos de seguridad mínimos | Activarlos con presupuesto |
| RA-06 | Puerto 80 abierto en el gateway (CKV_AWS_260) | Let's Encrypt usa el reto HTTP-01 | Caddy redirige todo el 80 a HTTPS; HSTS | Reto DNS-01 con dominio propio |
| RA-07 | Carga real (NBomber) y mutación (Stryker) fuera de la compuerta de cada PR | Correrlas en cada PR gasta capa gratuita y tarda más de una hora | `calidad-extendida.yml` cada lunes y manual antes de cada versión; en cada PR corre el presupuesto de latencia en memoria (p95 < 500 ms) | — (decisión de diseño) |
| RA-08 | URL fija de respaldo en `calidad-extendida.yml` (`52-3-200-211.sslip.io`) | No hay dominio propio; la IP es elástica y no cambia entre despliegues | Se sobrescribe con la variable del repositorio `PUBLIC_BASE_URL` | Comprar dominio |
| RA-09 | `DatabaseTest` se omite si el equipo no tiene Docker | Algunos equipos de desarrollo no tienen Docker | En CI (GitHub Actions) siempre hay Docker y la prueba corre | — |

## Cerrados el 2026-11-27 (sin costo)
- CKV2_AWS_61: ciclo de vida en los dos buckets (evidencias a 30 días, CloudTrail a 90).
- CKV2_AWS_32: la política de cabeceras de seguridad administrada ya está en CloudFront (`cdn.tf`); la excepción sigue en Checkov solo porque no reconoce políticas administradas referidas por ID.
- CKV2_AWS_12: grupo de seguridad por defecto de la VPC sin reglas.
