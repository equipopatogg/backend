# 007 — Evidencias y moderación

**Fecha:** 2026-11-07 · **Estado:** Escrita; por aprobar (PO) · **Sprint:** 4 (fila 99)
**Historias:** evidencias con foto o enlace, moderación antes de almacenar, Muro de la Fama, reportes y bloqueos (ID 149, ID 153).
**Endpoints del CRUD:** EVI-02, EVI-03 (hechos), EVI-04, EVI-05, EVI-06 (por hacer); REP-01..06 (hechos, [expediente](../REP-01-06-reports-blocks/spec.md)); EVI-01 no aplica (D-17).
**Fuentes:** constitución §3.8, §3.9, §6.4; CLARIFICACION #51–#70; Descripción del Backend 7.5 y 7.11; CRUD hojas Evidencias y Reportes; decisiones D-17, D-18, F2-D20.

## 1. Contexto y objetivo

El retado prueba que cumplió con una foto o un enlace. Nada inapropiado llega a S3 ni al grupo: la foto se modera en memoria antes de guardarse y el texto libre pasa por moderación. El grupo puede guardar las mejores fotos en el Muro de la Fama.

## 2. Fuera de alcance

- Video subido (solo enlaces), CameraX (cámara del sistema) y OCR (bandera `FEATURE_VISION_OCR` apagada).
- EVI-01 (URL firmada): reemplazada por una sola llamada multipart (D-17).
- Pantalla del administrador de la plataforma para revisar falsos positivos y apelaciones (CLARIFICACION #20: por correo).
- Vista previa oEmbed de enlaces (Q-012 abierta; no bloquea: el enlace se guarda sin título ni miniatura).

## 3. Requisitos funcionales (EARS)

| ID | Requisito | Endpoint |
|---|---|---|
| 007-R1 | Cuando el retado envíe una foto (JPEG, PNG o WebP, máximo 5 MB, validada por magic bytes, sin SVG) de un reto en `in_progress` o `voting` sin votos, el sistema deberá moderarla en memoria con Rekognition y, solo si se aprueba, reencodificarla sin EXIF y guardarla en S3. | EVI-02 |
| 007-R2 | Si Rekognition marca la foto (sexual explícito, violencia gráfica, odio o perturbador con confianza ≥ 80 %), entonces el sistema deberá responder `422 CONTENT_REJECTED` y no guardarla en ningún lado (007-D1). | EVI-02 |
| 007-R3 | Si el moderador no responde, entonces el sistema deberá responder `503` sin guardar nada (falla cerrada). | EVI-02 |
| 007-R4 | Cuando el retado envíe un enlace, el sistema deberá aceptarlo solo con HTTPS y dominio de la lista (youtube.com, youtu.be, twitch.tv, clips.twitch.tv, medal.tv, streamable.com, con `www.` y `m.`), sin descargarlo. | EVI-02 |
| 007-R5 | Mientras un reto tenga 3 evidencias, el sistema deberá rechazar otra con `409 EVIDENCE_LIMIT_REACHED`; la primera evidencia aprobada pasa el reto a `voting` (F2-D20). | EVI-02 |
| 007-R6 | Cuando un miembro del grupo pida las evidencias, el sistema deberá devolver solo las aprobadas con URL de CloudFront; las rechazadas o quitadas nunca se muestran. | EVI-03 |
| 007-R7 | Cuando el retado reemplace una evidencia mientras el reto no tenga votos, el sistema deberá aplicar las mismas reglas que EVI-02 y borrar de S3 la foto anterior; con votos, `409 VOTING_STARTED`. | EVI-04 |
| 007-R8 | Cuando el retado borre su evidencia antes del primer voto, el sistema deberá quitarla y borrarla de S3; si era la única, el reto vuelve a `in_progress` y se liberan los jurados (007-D2). Después del primer voto, solo el administrador del grupo puede quitarla (REP-04). | EVI-05 |
| 007-R9 | Cuando un miembro guarde en el Muro de la Fama una foto aprobada de un reto cumplido, el sistema deberá marcarla `saved_in_fame` y quitarle la fecha de borrado; la acción es idempotente. | EVI-06 |
| 007-R10 | Cuando una foto cumpla 30 días sin estar en el Muro de la Fama, el sistema deberá borrarla de S3 (CLARIFICACION #53). | Regla del bucket + tarea |
| 007-R11 | Cuando se modere texto libre (condición, recompensa, juego, nombre de grupo, apodo, comentario de voto), el sistema deberá revisar primero una lista local de palabras ofensivas en español y después Comprehend (toxicidad ≥ 0.7), porque Comprehend solo evalúa inglés (007-D3). | Todas las de texto |

## 4. Requisitos no funcionales

S3 privado con CloudFront + OAC; URLs del CDN sin datos personales; límite de evidencias por usuario al día; logs sin cuerpos de imagen, URLs firmadas ni etiquetas de moderación con contenido; una llamada a Rekognition por foto.

## 5. Endpoints y esquemas

EVI-02 `POST /contracts/{id}/evidence` (multipart `file` o JSON `{type:"link", url}`), EVI-03 `GET /contracts/{id}/evidence`, EVI-04 `PUT /contracts/{id}/evidence/{eid}` (mismo cuerpo que EVI-02), EVI-05 `DELETE /evidence/{eid}`, EVI-06 `POST /evidence/{eid}/save-fame`. Esquemas: `Evidence`, `CreateLinkEvidenceRequest`, `Error`.

## 6. Criterios de aceptación

| ID | Requisito | Escenario | Evidencia |
|---|---|---|---|
| 007-AC1 | R1, R4, R5 | Foto válida y enlace válido → 201; cuarta → 409; primera → `voting` | `CoreFlowTests`, `EVI-02` |
| 007-AC2 | R2, R3 | Foto marcada → 422 sin objeto en S3; moderador caído → 503 | `UnitTest/ImageModerationPolicyTests`, `FakeObjectStorage` |
| 007-AC3 | R1 | SVG, archivo de 6 MB o extensión falsa → 422; EXIF eliminado | `UnitTest/ImageProcessorTests` |
| 007-AC4 | R6 | Evidencia quitada no aparece en EVI-03 | `ReportsAndBlocksTests` |
| 007-AC5 | R7 | Reemplazo sin votos → 200 y objeto viejo borrado; con un voto → 409 | Por hacer (007-T3) |
| 007-AC6 | R8 | Borrar la única evidencia → reto a `in_progress`; ajeno → 403 | Por hacer (007-T4) |
| 007-AC7 | R9 | Guardar en la Fama dos veces → 200 ambas; foto de reto no cumplido → 409 | Por hacer (007-T5) |
| 007-AC8 | R10 | Regla de 30 días en el bucket excluye las fotos de la Fama | Por hacer (007-T6) |
| 007-AC9 | R11 | Una grosería en español en la condición → 422 sin llamar a Comprehend | Por hacer (007-T7) |

## 7. Decisiones (por aprobar)

- **007-D1:** la foto rechazada se descarta de inmediato, sin cuarentena de 24 h: la cuarentena solo sirve si existe quien revise los falsos positivos y en esta versión no hay administrador de la plataforma. El autor puede subir otra.
- **007-D2:** borrar la única evidencia regresa el reto a `in_progress` (antes de votos no hay resultado que proteger).
- **007-D3:** lista local de términos en español configurable en `Moderation:BlockedTerms`, revisada antes de Comprehend.

## 8. Preguntas abiertas

Q-012 (oEmbed) queda fuera de alcance; no hay otras.

## 9. Verificación contra la constitución

§3.8 sin video almacenado ✔ · §3.9 moderación antes de almacenar ✔ (y texto en español con 007-D3) · §6.4 tipos, 5 MB, magic bytes, EXIF, sin SVG, HTTPS y lista de dominios ✔ · §6.4 cuarentena: cambiada por 007-D1 (requiere aprobación) · §6.3 hasta 3 evidencias, reemplazables antes del primer voto ✔.

## Aprobación

| Rol | Persona | Fecha | Resultado |
|---|---|---|---|
| Product Owner | David M. | — | Por aprobar |
| Revisión técnica | Kevin H. | — | Pendiente |
| Comprobabilidad | José G. | — | Pendiente |

Expediente: [plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).
