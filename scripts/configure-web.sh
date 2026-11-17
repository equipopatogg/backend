#!/usr/bin/env bash
# Conecta la página web de verificación de correo con la API (F2-D75).
# Guarda en SSM Parameter Store el origen permitido por CORS y el enlace que va en el correo.
# No guarda secretos: son direcciones públicas.
#
# Uso (WSL):
#   cd /mnt/g/PactoGG && bash scripts/configure-web.sh
# Después hay que recrear las instancias para que lo lean:
#   bash scripts/deploy-first-time.sh        (muestra el plan y pide confirmación)
set -euo pipefail

PROFILE="${AWS_PROFILE:-pactogg}"
REGION="${AWS_REGION:-us-east-1}"
PREFIX="${SSM_PREFIX:-/pactogg/prod}"

put() { # nombre valor
  aws ssm put-parameter --profile "$PROFILE" --region "$REGION" \
    --name "$PREFIX/$1" --value "$2" --type String --overwrite >/dev/null
  echo "  guardado $PREFIX/$1"
}

DEFAULT_ORIGIN="https://black-river-065cb1710.6.azurestaticapps.net"
read -r -p "Origen de la página web, sin ruta ni diagonal final [$DEFAULT_ORIGIN]: " ORIGIN
ORIGIN="${ORIGIN:-$DEFAULT_ORIGIN}"
# Solo https, solo el origen: un comodín o una ruta abrirían la API a sitios que no son la página.
[[ "$ORIGIN" =~ ^https://[A-Za-z0-9.-]+(:[0-9]+)?$ ]] || { echo "Debe ser un origen https sin ruta, por ejemplo $DEFAULT_ORIGIN" >&2; exit 1; }

read -r -p "Ruta de la página de verificación [/verificar-correo]: " VERIFY_PATH
VERIFY_PATH="${VERIFY_PATH:-/verificar-correo}"
[[ "$VERIFY_PATH" =~ ^/[A-Za-z0-9/_-]+$ ]] || { echo "Ruta no válida." >&2; exit 1; }

echo "Guardando en SSM ($REGION, $PREFIX)..."
put Cors__AllowedOrigins__0 "$ORIGIN"
put Email__VerifyEmailLink "$ORIGIN$VERIFY_PATH"

read -r -p "¿Hay también página para restablecer contraseña? Escribe su ruta o deja vacío: " RESET_PATH
if [[ -n "$RESET_PATH" ]]; then
  [[ "$RESET_PATH" =~ ^/[A-Za-z0-9/_-]+$ ]] || { echo "Ruta no válida." >&2; exit 1; }
  put Email__ResetPasswordLink "$ORIGIN$RESET_PATH"
else
  echo "  sin cambios en Email__ResetPasswordLink (el correo de contraseña sigue abriendo la app)"
fi

echo
echo "Listo. El correo de verificación llevará: $ORIGIN$VERIFY_PATH?token=..."
echo "Falta recrear las instancias: bash scripts/deploy-first-time.sh"
