# Plan

- `Modules/Users/AvatarService.cs` reutiliza `ImageProcessor`, `IModerationService` e `IObjectStorage`.
- `User.AvatarKey` y `User.AvatarUrl`; `IUserRepository.SetAvatarPhotoAsync`; `UpdateProfileAsync` limpia la foto al elegir color.
- Endpoint en `UserEndpoints`, `openapi.yaml` y `docs/API-movil.md`.
