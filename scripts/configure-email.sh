#!/usr/bin/env bash
# Guarda la configuración de correo (SMTP) en SSM Parameter Store para la API (F2-D59).
# No escribe nada en el repositorio ni imprime la contraseña.
#
# Uso (WSL):
#   cd /mnt/g/PactoGG && bash scripts/configure-email.sh
# Después hay que recrear las instancias para que la lean:
#   bash scripts/deploy-first-time.sh        (muestra el plan y pide confirmación)
set -euo pipefail

PROFILE="${AWS_PROFILE:-pactogg}"
REGION="${AWS_REGION:-us-east-1}"
PREFIX="${SSM_PREFIX:-/pactogg/prod}"

put() { # nombre valor tipo
  aws ssm put-parameter --profile "$PROFILE" --region "$REGION" \
    --name "$PREFIX/$1" --value "$2" --type "$3" --overwrite >/dev/null
  echo "  guardado $PREFIX/$1"
}

read -r -p "Correo de Gmail que enviará los mensajes: " EMAIL
[[ "$EMAIL" == *@* ]] || { echo "Correo no válido." >&2; exit 1; }
read -r -s -p "Contraseña de aplicación de Google (16 letras, se pueden pegar con espacios): " APP_PASSWORD
echo
APP_PASSWORD="${APP_PASSWORD// /}"
[[ ${#APP_PASSWORD} -eq 16 ]] || { echo "La contraseña de aplicación debe tener 16 letras." >&2; exit 1; }

echo "Guardando en SSM ($REGION, $PREFIX)..."
put Email__SmtpHost smtp.gmail.com String
put Email__SmtpPort 587 String
put Email__SmtpUser "$EMAIL" String
put Email__FromAddress "$EMAIL" String
put Email__SmtpPassword "$APP_PASSWORD" SecureString
unset APP_PASSWORD

read -r -p "¿Exigir correo verificado para crear o unirse a grupos (CLARIFICACION #5)? [s/N]: " VERIFY
if [[ "${VERIFY,,}" == s* ]]; then
  put Auth__RequireVerifiedEmail true String
else
  echo "  sin cambios en Auth__RequireVerifiedEmail (se puede activar después volviendo a correr este script)"
fi

echo "Listo. Recrea las instancias para que lo lean: bash scripts/deploy-first-time.sh"
