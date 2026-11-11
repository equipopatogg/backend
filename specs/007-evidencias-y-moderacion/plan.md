# Plan — 007 Evidencias y moderación

[Spec](spec.md).

## Enfoque

EVI-02 y EVI-03 ya existen; se agregan EVI-04/05/06 en `Modules/Evidence` reutilizando la validación, la moderación y el almacenamiento de EVI-02. La lista local de términos en español entra como un paso previo dentro de `IModerationService.ModerateTextAsync`, así cubre todos los textos sin tocar cada servicio.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Modules/Evidence/EvidenceService.cs`, `EvidenceEndpoints.cs` | EVI-04, EVI-05, EVI-06 |
| `Pactogg.Api/Modules/Evidence/Abstractions/IEvidenceRepository.cs` + Mongo + fake | Reemplazar, borrar, guardar en la Fama |
| `Pactogg.Api/Infrastructure/Moderation/*` | Lista `Moderation:BlockedTerms` antes de Comprehend |
| `deploy/terraform/storage.tf` | Regla de 30 días en `evidence/`; fotos de la Fama copiadas a `fame/` |
| `IntegrationTest/EvidenceManagementTests.cs`, `UnitTest/BlockedTermsTests.cs` | Pruebas 007-AC5..AC9 |
| `api/openapi.yaml`, `docs/API-movil.md` | Contrato |

## Riesgos

Cambiar `storage.tf` es un `terraform apply` sin reemplazo de instancias; la lista de palabras puede dar falsos positivos (se mantiene corta y configurable).
