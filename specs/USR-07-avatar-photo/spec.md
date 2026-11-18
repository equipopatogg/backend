# USR-07 — Foto de perfil

Fecha: 2026-11-17. Estado: **APROBADA POR EL PO** (pidió poder cambiar el avatar por una imagen). Decisión F2-D77.

## Requisitos
- R1: `POST /api/v1/users/me/avatar` con multipart `file` (JPEG, PNG o WebP, ≤ 5 MB).
- R2: La imagen se vuelve a codificar como JPEG sin metadatos y Rekognition la revisa antes de guardarla; si la rechaza → `422 CONTENT_REJECTED`; si no responde → `503` y no se guarda nada.
- R3: Se guarda en `avatars/{user_id}/{id}.jpg`, fuera de la regla de 30 días de `evidence/`.
- R4: El perfil (USR-02/03), el perfil público (USR-05) y la lista de miembros (MEM-02) devuelven `avatar_id = "photo"` y `avatar_url`.
- R5: Elegir un color con `PATCH users/me` quita la foto y la borra de S3; `avatar_id = "photo"` por PATCH → 422.
- R6: Eliminar la cuenta borra la foto de S3.

## Criterios
| ID | Resultado | Prueba |
|---|---|---|
| USR-07-AC1 | Subir una foto la muestra en el perfil y en los miembros del grupo | `AvatarTests.UploadedPhoto_IsShownInProfileAndMembers_AndColorRemovesIt` |
| USR-07-AC2 | Elegir un color quita la foto y la borra de S3 | La misma |
| USR-07-AC3 | Archivo de otro tipo o rechazado por moderación no se guarda | `AvatarTests.RejectedOrWrongFile_IsNotStored` |
