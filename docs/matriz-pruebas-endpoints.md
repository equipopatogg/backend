# Matriz de pruebas por endpoint

Generada con `python3 scripts/matriz_pruebas.py` a partir de `api/openapi.yaml` y del código de las pruebas.

- **Unitarias:** reglas puras del módulo (no necesitan HTTP ni base de datos).
- **Integración:** pruebas que llaman a esa ruta con la API en memoria (`WebApplicationFactory`).
- **Seguridad:** el barrido recorre las rutas registradas en la API, no una lista a mano.

Endpoints: **96** · con prueba de integración: **96** · sin integración: **0**

| Método | Ruta | Unitarias | Integración | Seguridad |
|---|---|---|---|---|
| GET | `/health` | — | HealthTests, SddDecisionTests | SecurityHeadersTests |
| GET | `/health/ready` | — | SddDecisionTests | — |
| POST | `/api/v1/auth/register` | PasswordHasherTests, GoogleTokenValidatorTests | AccountAndPushTests, CoreFlowTests, DocComplianceTests, FunctionalFlowChecks, GoogleAuthTests, JuryFlowTests, SocialFlowTests | EndpointAuthorizationSweepTests (anónimo permitido) |
| POST | `/api/v1/auth/login` | PasswordHasherTests, GoogleTokenValidatorTests | AccountAndPushTests, CoreFlowTests, DocComplianceTests, FunctionalFlowChecks, GoogleAuthTests | EndpointAuthorizationSweepTests (anónimo permitido) |
| POST | `/api/v1/auth/google` | PasswordHasherTests, GoogleTokenValidatorTests | GoogleAuthTests | EndpointAuthorizationSweepTests (anónimo permitido) |
| POST | `/api/v1/auth/refresh` | PasswordHasherTests, GoogleTokenValidatorTests | AccountAndPushTests, DocComplianceTests | EndpointAuthorizationSweepTests (anónimo permitido) |
| POST | `/api/v1/auth/logout` | PasswordHasherTests, GoogleTokenValidatorTests | CoreFlowTests, FunctionalFlowChecks, Spec006PushTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/auth/verify-email` | PasswordHasherTests, GoogleTokenValidatorTests | AccountAndPushTests | EndpointAuthorizationSweepTests (anónimo permitido) |
| POST | `/api/v1/auth/verify-email/resend` | PasswordHasherTests, GoogleTokenValidatorTests | AccountAndPushTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/auth/forgot-password` | PasswordHasherTests, GoogleTokenValidatorTests | AccountAndPushTests | EndpointAuthorizationSweepTests (anónimo permitido) |
| POST | `/api/v1/auth/reset-password` | PasswordHasherTests, GoogleTokenValidatorTests | AccountAndPushTests | EndpointAuthorizationSweepTests (anónimo permitido) |
| GET | `/api/v1/users/me` | ImageModerationPolicyTests, ImageProcessorTests, TextModerationPolicyTests | AccountAndPushTests, AvatarTests, DeleteAccountTests, DocComplianceTests, JuryFlowTests, SocialFlowTests, Spec006And009Tests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| PATCH | `/api/v1/users/me` | ImageModerationPolicyTests, ImageProcessorTests, TextModerationPolicyTests | AccountAndPushTests, AvatarTests, DeleteAccountTests, DocComplianceTests, JuryFlowTests, SocialFlowTests, Spec006And009Tests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| DELETE | `/api/v1/users/me` | ImageModerationPolicyTests, ImageProcessorTests, TextModerationPolicyTests | AccountAndPushTests, AvatarTests, DeleteAccountTests, DocComplianceTests, JuryFlowTests, SocialFlowTests, Spec006And009Tests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| POST | `/api/v1/users/me/avatar` | ImageModerationPolicyTests, ImageProcessorTests, TextModerationPolicyTests | AvatarTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| GET | `/api/v1/users/check-nickname` | ImageModerationPolicyTests, ImageProcessorTests, TextModerationPolicyTests | SocialFlowTests | EndpointAuthorizationSweepTests (anónimo permitido) |
| GET | `/api/v1/users/{id}` | ImageModerationPolicyTests, ImageProcessorTests, TextModerationPolicyTests | AccountAndPushTests, AvatarTests, DeleteAccountTests, DocComplianceTests, JuryFlowTests, SocialFlowTests, Spec006And009Tests, Spec008ReputationTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| POST | `/api/v1/users/me/password` | ImageModerationPolicyTests, ImageProcessorTests, TextModerationPolicyTests | AccountAndPushTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/users/me/consent` | ImageModerationPolicyTests, ImageProcessorTests, TextModerationPolicyTests | AccountAndPushTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| GET | `/api/v1/groups` | TextModerationPolicyTests | AccountAndPushTests, CoreFlowTests, DocComplianceTests, FunctionalFlowChecks, GroupAdminTests, JuryFlowTests, SddDecisionTests, SocialFlowTests, Spec007EvidenceTests, Spec010MatchTests, Spec010TournamentTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| POST | `/api/v1/groups` | TextModerationPolicyTests | AccountAndPushTests, CoreFlowTests, DocComplianceTests, FunctionalFlowChecks, GroupAdminTests, JuryFlowTests, SddDecisionTests, SocialFlowTests, Spec007EvidenceTests, Spec010MatchTests, Spec010TournamentTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| POST | `/api/v1/groups/join` | TextModerationPolicyTests | AccountAndPushTests, GroupAdminTests, JuryFlowTests, SocialFlowTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| GET | `/api/v1/groups/{id}` | TextModerationPolicyTests | AccountAndPushTests, DeleteAccountTests, GroupAdminTests, JuryFlowTests, SocialFlowTests, Spec010MatchTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| PATCH | `/api/v1/groups/{id}` | TextModerationPolicyTests | AccountAndPushTests, DeleteAccountTests, GroupAdminTests, JuryFlowTests, SocialFlowTests, Spec010MatchTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| DELETE | `/api/v1/groups/{id}` | TextModerationPolicyTests | AccountAndPushTests, DeleteAccountTests, GroupAdminTests, JuryFlowTests, SocialFlowTests, Spec010MatchTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| GET | `/api/v1/groups/{id}/members` | TextModerationPolicyTests | AvatarTests, SocialFlowTests, Spec006And009Tests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| POST | `/api/v1/contracts` | ContractRulesTests, TextModerationPolicyTests | CoreFlowTests, FunctionalFlowChecks | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| GET | `/api/v1/groups/{gid}/contracts` | ContractRulesTests, TextModerationPolicyTests | AccountAndPushTests, CoreFlowTests, JuryFlowTests, ReportsAndBlocksTests, SocialFlowTests, Spec004ContractTests, Spec005ContractDraftTests, Spec006And009Tests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| POST | `/api/v1/groups/{gid}/contracts` | ContractRulesTests, TextModerationPolicyTests | AccountAndPushTests, CoreFlowTests, JuryFlowTests, ReportsAndBlocksTests, SocialFlowTests, Spec004ContractTests, Spec005ContractDraftTests, Spec006And009Tests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| GET | `/api/v1/contracts/{id}` | ContractRulesTests, TextModerationPolicyTests | AccountAndPushTests, DeleteAccountTests, GroupAdminTests, SocialFlowTests, Spec004ContractTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| PATCH | `/api/v1/contracts/{id}` | ContractRulesTests, TextModerationPolicyTests | AccountAndPushTests, DeleteAccountTests, GroupAdminTests, SocialFlowTests, Spec004ContractTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| DELETE | `/api/v1/contracts/{id}` | ContractRulesTests, TextModerationPolicyTests | AccountAndPushTests, DeleteAccountTests, GroupAdminTests, SocialFlowTests, Spec004ContractTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| POST | `/api/v1/groups/{gid}/contracts/parse` | ContractRulesTests, TextModerationPolicyTests | Spec005ContractDraftTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/contracts/{id}/cancel` | ContractRulesTests, TextModerationPolicyTests | Spec004AdminCancelTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/contracts/{id}/accept` | ContractRulesTests, TextModerationPolicyTests | AccountAndPushTests, DeleteAccountTests, DetailEndpointsTests, GroupAdminTests, JuryFlowTests, ReportsAndBlocksTests, SocialFlowTests, Spec004AdminCancelTests, Spec007EvidenceTests, Spec008ReputationTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| POST | `/api/v1/contracts/{id}/reject` | ContractRulesTests, TextModerationPolicyTests | SocialFlowTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| GET | `/api/v1/contracts/{id}/evidence` | EvidenceUrlValidatorTests, ImageModerationPolicyTests, ImageProcessorTests | CoreFlowTests, FunctionalFlowChecks, JuryFlowTests, ReportsAndBlocksTests, Spec004AdminCancelTests, Spec007EvidenceTests, Spec008ReputationTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| POST | `/api/v1/contracts/{id}/evidence` | EvidenceUrlValidatorTests, ImageModerationPolicyTests, ImageProcessorTests | CoreFlowTests, FunctionalFlowChecks, JuryFlowTests, ReportsAndBlocksTests, Spec004AdminCancelTests, Spec007EvidenceTests, Spec008ReputationTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| PUT | `/api/v1/contracts/{id}/evidence/{eid}` | EvidenceUrlValidatorTests, ImageModerationPolicyTests, ImageProcessorTests | Spec007EvidenceTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| DELETE | `/api/v1/evidence/{eid}` | EvidenceUrlValidatorTests, ImageModerationPolicyTests, ImageProcessorTests | Spec007EvidenceTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/evidence/{eid}/save-fame` | EvidenceUrlValidatorTests, ImageModerationPolicyTests, ImageProcessorTests | Spec007EvidenceTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| GET | `/api/v1/groups/{gid}/shame` | — | Spec008ReputationTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| GET | `/api/v1/users/{id}/achievements` | — | Spec008ReputationTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| GET | `/api/v1/tournaments` | TextModerationPolicyTests | CoreFlowTests, FunctionalFlowChecks, Spec010TournamentTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| GET | `/api/v1/groups/{gid}/tournaments` | TextModerationPolicyTests | CoreFlowTests, Spec010MatchTests, Spec010TournamentTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/groups/{gid}/tournaments` | TextModerationPolicyTests | CoreFlowTests, Spec010MatchTests, Spec010TournamentTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| GET | `/api/v1/tournaments/{id}` | TextModerationPolicyTests | Spec010MatchTests, Spec010TournamentTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| PATCH | `/api/v1/tournaments/{id}` | TextModerationPolicyTests | Spec010MatchTests, Spec010TournamentTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/tournaments/{id}/cancel` | TextModerationPolicyTests | Spec010TournamentTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/tournaments/{id}/join` | TextModerationPolicyTests | Spec010MatchTests, Spec010TournamentTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| DELETE | `/api/v1/tournaments/{id}/join` | TextModerationPolicyTests | Spec010MatchTests, Spec010TournamentTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/tournaments/{id}/invite` | TextModerationPolicyTests | Spec010MatchTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/tournaments/{id}/start` | TextModerationPolicyTests | Spec010MatchTests, Spec010TournamentTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| GET | `/api/v1/tournaments/{id}/bracket` | TextModerationPolicyTests | Spec010TournamentTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| GET | `/api/v1/matches/{id}` | TextModerationPolicyTests | Spec010TournamentTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/matches/{id}/report` | TextModerationPolicyTests | Spec010MatchTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/matches/{id}/confirm` | TextModerationPolicyTests | Spec010MatchTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/matches/{id}/dispute` | TextModerationPolicyTests | Spec010MatchTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/matches/{id}/resolve` | TextModerationPolicyTests | Spec010MatchTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| GET | `/api/v1/me/matches/next` | TextModerationPolicyTests | Spec010TournamentTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| GET | `/api/v1/me/votes/pending` | VotingRulesTests | JuryFlowTests, Spec007EvidenceTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| PUT | `/api/v1/contracts/{id}/votes` | VotingRulesTests | JuryFlowTests, Spec007EvidenceTests, Spec008ReputationTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| GET | `/api/v1/contracts/{id}/votes/summary` | VotingRulesTests | JuryFlowTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| POST | `/api/v1/contracts/{id}/votes/resolve` | VotingRulesTests | JuryFlowTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| GET | `/api/v1/contracts/{id}/result` | ContractRulesTests, TextModerationPolicyTests | JuryFlowTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| GET | `/api/v1/groups/{gid}/points/me` | — | JuryFlowTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| GET | `/api/v1/groups/{gid}/points/ranking` | — | JuryFlowTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| GET | `/api/v1/groups/{gid}/commitments` | — | JuryFlowTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| POST | `/api/v1/commitments/{id}/complete` | — | JuryFlowTests, Spec006And009Tests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| POST | `/api/v1/commitments/{id}/request-complete` | — | JuryFlowTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| GET | `/api/v1/notifications` | — | AccountAndPushTests, JuryFlowTests, Spec004AdminCancelTests, Spec006And009Tests, Spec010MatchTests, Spec010TournamentTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| DELETE | `/api/v1/notifications/{id}` | — | AccountAndPushTests, JuryFlowTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| PATCH | `/api/v1/notifications/{id}/read` | — | JuryFlowTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| POST | `/api/v1/notifications/read-all` | — | JuryFlowTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| GET | `/api/v1/groups/{gid}/fame` | — | JuryFlowTests | EndpointAuthorizationSweepTests (sin token y token falso → 401), AuthorizationTests |
| PUT | `/api/v1/devices/me` | PushPreferencesTests | AccountAndPushTests, DeleteAccountTests, Spec006PushTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| DELETE | `/api/v1/devices/me` | PushPreferencesTests | AccountAndPushTests, DeleteAccountTests, Spec006PushTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| GET | `/api/v1/devices/me/preferences` | PushPreferencesTests | AccountAndPushTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| PATCH | `/api/v1/devices/me/preferences` | PushPreferencesTests | AccountAndPushTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| GET | `/api/v1/groups/{gid}/audit` | — | AccountAndPushTests, Spec004AdminCancelTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/groups/{id}/invite-code/regenerate` | TextModerationPolicyTests | GroupAdminTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| PATCH | `/api/v1/groups/{id}/invite-code` | TextModerationPolicyTests | GroupAdminTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| GET | `/api/v1/groups/{id}/members/me` | TextModerationPolicyTests | GroupAdminTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| DELETE | `/api/v1/groups/{id}/members/me` | TextModerationPolicyTests | GroupAdminTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| PATCH | `/api/v1/groups/{id}/members/{uid}` | TextModerationPolicyTests | GroupAdminTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| DELETE | `/api/v1/groups/{id}/members/{uid}` | TextModerationPolicyTests | GroupAdminTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| GET | `/api/v1/points/movements/{id}` | — | DetailEndpointsTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| GET | `/api/v1/groups/{gid}/ranking` | — | DetailEndpointsTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| GET | `/api/v1/commitments/{id}` | — | DetailEndpointsTests, Spec006And009Tests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| GET | `/api/v1/groups/{gid}/commitments/summary` | — | DetailEndpointsTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/reports` | — | ReportsAndBlocksTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| GET | `/api/v1/groups/{gid}/reports` | — | ReportsAndBlocksTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/reports/{id}/resolve` | — | ReportsAndBlocksTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| DELETE | `/api/v1/groups/{gid}/content/{type}/{id}` | TextModerationPolicyTests | ReportsAndBlocksTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| POST | `/api/v1/users/{id}/block` | — | ReportsAndBlocksTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
| DELETE | `/api/v1/users/{id}/block` | — | ReportsAndBlocksTests | EndpointAuthorizationSweepTests (sin token y token falso → 401) |
