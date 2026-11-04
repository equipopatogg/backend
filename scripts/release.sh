#!/usr/bin/env bash
# Publica una versión nueva del backend en las 2 instancias SIN tocar la infraestructura.
#
#   cd /mnt/g/PactoGG && bash scripts/release.sh            # pruebas + build + push + redeploy + smoke
#   SKIP_TESTS=1 bash scripts/release.sh                    # sin dotnet test (no recomendado)
#
# Pasos: dotnet test -> docker build/push a ECR (tag fecha + latest) -> SSM redeploy en ambas
# instancias (una por una, para no dejar el servicio sin instancias) -> /health.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TF_DIR="$REPO_ROOT/deploy/terraform"
export PATH="$HOME/.local/pactogg-tools/bin:/tmp/opencode/bin:/tmp/opencode/terraform:/tmp/opencode/dotnet:$HOME/.dotnet:$PATH"
export AWS_PROFILE="${AWS_PROFILE:-pactogg}" AWS_REGION="${AWS_REGION:-us-east-1}" AWS_PAGER=""

step() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }
fail() { printf '\n\033[1;31mERROR:\033[0m %s\n' "$*" >&2; exit 1; }

# ---------------------------------------------------------------------------
step "1/4 Pruebas"
if [[ "${SKIP_TESTS:-0}" == "1" ]]; then
  echo "Omitidas (SKIP_TESTS=1)."
else
  if ! command -v dotnet >/dev/null 2>&1; then
    echo "Instalando .NET 8 SDK en ~/.dotnet ..."
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
    bash /tmp/dotnet-install.sh --channel 8.0 --install-dir "$HOME/.dotnet"
  fi
  export DOTNET_ROOT="$(dirname "$(readlink -f "$(command -v dotnet)")")"
  export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
  cd "$REPO_ROOT"
  dotnet build Pactogg.sln -c Release
  dotnet test Pactogg.sln -c Release --no-build --logger "console;verbosity=minimal" \
    || fail "Hay pruebas en rojo; no se publica."
fi

# ---------------------------------------------------------------------------
step "2/4 Imagen Docker -> ECR"
cd "$TF_DIR"
ECR_URL="$(terraform output -raw ecr_repository_url)"
API_URL="$(terraform output -raw api_url)"
INSTANCE_IDS="$(terraform output -json instance_ids | python3 -c 'import json,sys; print(" ".join(json.load(sys.stdin)))')"
DOCKER="docker"; CONTEXT="$REPO_ROOT"
if ! docker info >/dev/null 2>&1; then
  DOCKER="/mnt/c/Program Files/Docker/Docker/resources/bin/docker.exe"
  CONTEXT="$(wslpath -w "$REPO_ROOT")"
fi
TAG="$(date +%Y%m%d%H%M%S)"
for i in 1 2 3; do
  aws ecr get-login-password | "$DOCKER" login --username AWS --password-stdin "${ECR_URL%%/*}" && break
  echo "Reintento login ECR ($i)..."; sleep 5
done
"$DOCKER" build -t "$ECR_URL:latest" -t "$ECR_URL:$TAG" "$CONTEXT"
for t in "$TAG" latest; do
  for i in 1 2 3; do "$DOCKER" push "$ECR_URL:$t" && break; echo "Reintento push ($i)..."; sleep 5; done
done

# ---------------------------------------------------------------------------
step "3/4 Redespliegue por SSM (una instancia a la vez)"
for id in $INSTANCE_IDS; do
  INSTANCE_IDS="$id" bash "$REPO_ROOT/scripts/deploy-aws.sh"
done

# ---------------------------------------------------------------------------
step "4/4 Smoke $API_URL/health"
for i in $(seq 1 12); do
  code="$(curl -s -m 8 -o /dev/null -w '%{http_code}' "$API_URL/health" || true)"
  [[ "$code" == "200" ]] && { echo "OK: versión $TAG publicada en $API_URL"; exit 0; }
  sleep 10
done
fail "/health no respondió 200 tras el redespliegue."
