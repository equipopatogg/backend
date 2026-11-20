# Cambios de la API PactoGG

Versión semántica (mayor.menor.parche). La versión de la API está en `GET /` y en la imagen Docker (etiqueta por SHA/fecha); la ruta pública es `/api/v1`. El detalle de cada versión y sus decisiones está en `Memoria.md` (sección 9).

## 1.0.0 — 2026-11-19
- Barrido de seguridad 401 sobre todas las rutas registradas y token falsificado; salud sin fugas.
- Pruebas unitarias de servicio para avisos, puntos, compromisos, Fama, reportes/bloqueos, reputación y auditoría.
- Revocación de tokens que falla cerrada sin Redis en producción.
- CloudTrail y alarmas de CloudWatch con aviso por SNS.
- Matriz endpoint → pruebas (`docs/matriz-pruebas-endpoints.md`).

## 0.14.0 — 2026-11-18
- Contrato por voz con Groq (`openai/gpt-oss-20b`).

## 0.13.0 — 2026-11-17
- Moderación de texto adaptada a videojuegos; foto de perfil moderada (USR-07).

## 0.12.x — 2026-11-12/13
- Torneos parte 2 y contrato por voz con adaptador de IA; despliegue solo `linux-x64`.

## 0.1.0 a 0.11.1 — 2026-10-30 a 2026-11-11
- Base SDD, autenticación, grupos, retos, evidencias, jurado, puntos, compromisos, avisos, torneos parte 1, Terraform y CI/CD. Ver `Memoria.md`.
