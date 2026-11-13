#!/usr/bin/env bash
# Conecta el modelo de IA del contrato por voz (CON-08, spec 005, F2-D73) guardando su configuración en SSM.
# No escribe nada en el repositorio ni imprime la clave.
#
# Sirve para cualquier API compatible con OpenAI (/chat/completions): Groq, OpenRouter, Mistral, Ollama,
# Gemini en modo OpenAI o el modelo que entregue el equipo si expone ese formato.
#
# Uso (WSL):
#   cd /mnt/g/PactoGG && bash scripts/configure-ai.sh
# Después hay que recrear las instancias para que lo lean:
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

read -r -p "URL base del proveedor (la parte antes de /chat/completions, por ejemplo https://api.groq.com/openai/v1): " ENDPOINT
[[ "$ENDPOINT" == https://* ]] || { echo "La URL debe empezar con https://" >&2; exit 1; }
ENDPOINT="${ENDPOINT%/}"
ENDPOINT="${ENDPOINT%/chat/completions}"
read -r -p "Nombre del modelo: " MODEL
[[ -n "$MODEL" ]] || { echo "Falta el nombre del modelo." >&2; exit 1; }
read -r -s -p "Clave de la API (Enter si el modelo no pide clave): " API_KEY
echo

echo "Guardando en SSM ($REGION, $PREFIX)..."
put Ai__Provider OpenAiCompatible String
put Ai__Endpoint "$ENDPOINT" String
put Ai__Model "$MODEL" String
if [[ -n "$API_KEY" ]]; then
  put Ai__ApiKey "$API_KEY" SecureString
fi
unset API_KEY

echo "Listo. Recrea las instancias para que lo lean: bash scripts/deploy-first-time.sh"
echo "Para apagar la IA: aws ssm delete-parameter --name $PREFIX/Ai__Provider (la app vuelve a escribir a mano)."
