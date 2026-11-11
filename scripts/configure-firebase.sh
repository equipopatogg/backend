#!/usr/bin/env bash
# Guarda la cuenta de servicio de Firebase en SSM Parameter Store para el push (FCM).
# El JSON trae una clave privada: no se imprime, no se copia al repositorio y no va en la app Android.
#
# Uso (WSL):
#   cd /mnt/g/PactoGG
#   bash scripts/configure-firebase.sh "/mnt/c/Users/david/Downloads/pactogg-firebase-adminsdk-fbsvc-b0aee067bd.json"
# Después, para que la API lo lea sin recrear instancias:
#   bash scripts/release.sh
set -euo pipefail

PROFILE="${AWS_PROFILE:-pactogg}"
REGION="${AWS_REGION:-us-east-1}"
PREFIX="${SSM_PREFIX:-/pactogg/prod}"
FILE="${1:-}"

[[ -f "$FILE" ]] || { echo "Pasa la ruta del JSON de la cuenta de servicio (el de firebase-adminsdk)." >&2; exit 1; }

# Una sola línea (el archivo de variables de Docker no admite saltos de línea) y validación mínima.
COMPACT="$(python3 - "$FILE" <<'PY'
import json, sys
data = json.load(open(sys.argv[1], encoding="utf-8"))
if data.get("type") != "service_account" or "private_key" not in data or "client_email" not in data:
    sys.exit("Ese archivo no es una cuenta de servicio (¿es google-services.json? ese va en la app).")
print(json.dumps(data, separators=(",", ":")))
PY
)"
PROJECT_ID="$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["project_id"])' "$COMPACT")"

if (( ${#COMPACT} > 4096 )); then
  echo "El JSON mide ${#COMPACT} caracteres; Parameter Store estándar admite 4096." >&2
  exit 1
fi

put() {
  aws ssm put-parameter --profile "$PROFILE" --region "$REGION" \
    --name "$PREFIX/$1" --value "$2" --type "$3" --overwrite >/dev/null
  echo "  guardado $PREFIX/$1"
}

echo "Guardando en SSM ($REGION, $PREFIX)..."
put Firebase__ServiceAccountJson "$COMPACT" SecureString
put Firebase__ProjectId "$PROJECT_ID" String
unset COMPACT
echo "Listo (proyecto $PROJECT_ID). Ahora: bash scripts/release.sh"
