#!/usr/bin/env python3
"""Genera docs/matriz-pruebas-endpoints.md: cada endpoint de api/openapi.yaml con las pruebas
que lo ejercitan (integración por ruta, seguridad por barrido y unitarias por módulo).
Uso: python3 scripts/matriz_pruebas.py  (desde la raíz del repo)."""
import glob, re, sys, yaml
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
spec = yaml.safe_load((ROOT / "api/openapi.yaml").read_text(encoding="utf-8"))
METHODS = ("get", "post", "put", "patch", "delete")

def tests_in(project):
    files = {}
    for f in glob.glob(str(ROOT / project / "**/*.cs"), recursive=True):
        if "/bin/" in f or "/obj/" in f or "/Fakes/" in f:
            continue
        files[Path(f).stem] = Path(f).read_text(encoding="utf-8")
    return files

integration = tests_in("IntegrationTest")
security = tests_in("SecurityTest")
unit = tests_in("UnitTest")
anonymous = {"/api/v1/auth/register", "/api/v1/auth/login", "/api/v1/auth/google", "/api/v1/auth/refresh",
             "/api/v1/auth/verify-email", "/api/v1/auth/forgot-password", "/api/v1/auth/reset-password",
             "/api/v1/users/check-nickname"}

# Reglas de negocio por módulo que cubren las pruebas unitarias.
UNIT_BY_MODULE = {
    "auth": ["PasswordHasherTests", "GoogleTokenValidatorTests", "TokenRevocationTests"],
    "notifications": ["NotificationServiceTests"],
    "points": ["PointsServiceTests"],
    "reports": ["ReportServiceTests"],
    "reputation": ["ReputationRulesTests"],
    "commitments": ["CommitmentAndFameServiceTests"],
    "fame": ["CommitmentAndFameServiceTests"],
    "audit": ["AuditQueryServiceTests"],
    "health": ["(no aplica: sin lógica propia; HealthTests y HealthExposureTests)"],
    "contracts": ["ContractRulesTests", "TextModerationPolicyTests"],
    "evidence": ["EvidenceUrlValidatorTests", "ImageModerationPolicyTests", "ImageProcessorTests"],
    "votes": ["VotingRulesTests"],
    "devices": ["PushPreferencesTests"],
    "users": ["ImageModerationPolicyTests", "ImageProcessorTests", "TextModerationPolicyTests"],
    "groups": ["TextModerationPolicyTests"],
    "tournaments": ["TextModerationPolicyTests"],
}

def module(path):
    parts = path.split("/")
    if path.startswith("/health"):
        return "health"
    if "evidence" in parts:
        return "evidence"
    if "votes" in parts:
        return "votes"
    for name in ("tournaments", "matches", "commitments", "points", "ranking", "fame", "shame", "reports",
                 "audit", "notifications", "devices", "achievements", "blocks", "block", "avatar", "contracts"):
        if name in parts:
            return {"matches": "tournaments", "ranking": "points", "shame": "reputation",
                    "achievements": "reputation", "blocks": "reports", "block": "reports",
                    "avatar": "users"}.get(name, name)
    return parts[3] if len(parts) > 3 else "otros"

def pattern(path):
    regex = re.escape(path)
    regex = re.sub(r"\\\{[^}]+\\\}", r"(\\{[^}]+\\}|[A-Za-z0-9_\\-]+)", regex)
    return re.compile(regex + r'(["?]|$)', re.M)

rows, missing = [], []
for path, item in spec["paths"].items():
    for method in METHODS:
        if method not in item:
            continue
        rx = pattern(path)
        integ = sorted(name for name, code in integration.items() if rx.search(code))
        explicit = sorted(name for name, code in security.items() if rx.search(code))
        sec = []
        if path.startswith("/api") and path not in anonymous:
            sec.append("EndpointAuthorizationSweepTests (sin token y token falso → 401)")
        sec += [name for name in explicit if name != "EndpointAuthorizationSweepTests"]
        if path in anonymous:
            sec.append("EndpointAuthorizationSweepTests (anónimo permitido)")
        units = UNIT_BY_MODULE.get(module(path), [])
        if not integ:
            missing.append(f"{method.upper()} {path}")
        rows.append((method.upper(), path, ", ".join(units) or "—", ", ".join(integ) or "**falta**",
                     ", ".join(sec) or "—"))

out = ["# Matriz de pruebas por endpoint", "",
       "Generada con `python3 scripts/matriz_pruebas.py` a partir de `api/openapi.yaml` y del código de las pruebas.",
       "", "- **Unitarias:** reglas puras del módulo (no necesitan HTTP ni base de datos).",
       "- **Integración:** pruebas que llaman a esa ruta con la API en memoria (`WebApplicationFactory`).",
       "- **Seguridad:** el barrido recorre las rutas registradas en la API, no una lista a mano.", "",
       f"Endpoints: **{len(rows)}** · con prueba de integración: **{len(rows) - len(missing)}** · "
       f"sin integración: **{len(missing)}**", "",
       "| Método | Ruta | Unitarias | Integración | Seguridad |", "|---|---|---|---|---|"]
out += [f"| {m} | `{p}` | {u} | {i} | {s} |" for m, p, u, i, s in rows]
if missing:
    out += ["", "## Sin prueba de integración", ""] + [f"- `{x}`" for x in missing]
(ROOT / "docs/matriz-pruebas-endpoints.md").write_text("\n".join(out) + "\n", encoding="utf-8")
print(f"{len(rows)} endpoints, {len(missing)} sin integración")
for x in missing:
    print("  ", x)
sys.exit(0)
