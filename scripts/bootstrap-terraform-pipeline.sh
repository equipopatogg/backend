#!/usr/bin/env bash
# Prepara la cuenta para CREAR y ELIMINAR la infraestructura desde el pipeline (workflow infra.yml).
# Se corre UNA sola vez desde WSL, con la infraestructura actual funcionando:
#
#   cd /mnt/g/PactoGG && bash scripts/bootstrap-terraform-pipeline.sh
#
# 1. Asegura Terraform >= 1.10 (bloqueo nativo de S3).
# 2. Aplica deploy/bootstrap: bucket del estado, proveedor OIDC de GitHub, rol pactogg-github-terraform,
#    AWS Budgets (alertas 50/80/100 %) y CloudTrail. Pide el correo de alertas si no viene en BUDGET_ALERT_EMAIL.
#    Si el proveedor OIDC ya existe (lo creó deploy/terraform), lo importa en vez de duplicarlo.
# 3. Migra el estado local de deploy/terraform al bucket S3 y saca de él el proveedor OIDC.
# 4. Muestra el plan (debe salir sin cambios o casi) y configura los secretos de GitHub si tienes `gh`.
# No borra ni recrea recursos de la app.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TF_DIR="$REPO_ROOT/deploy/terraform"
BOOT_DIR="$REPO_ROOT/deploy/bootstrap"
TOOLS_DIR="$HOME/.local/pactogg-tools"
TF_VERSION="1.10.5"
GITHUB_REPO="equipopatogg/backend"
export PATH="$TOOLS_DIR/bin:$HOME/.dotnet:$PATH"
export AWS_PROFILE="${AWS_PROFILE:-pactogg}" AWS_REGION="${AWS_REGION:-us-east-1}" AWS_PAGER=""

step() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }
fail() { printf '\n\033[1;31mERROR:\033[0m %s\n' "$*" >&2; exit 1; }

# ---------------------------------------------------------------------------
step "1/5 Terraform >= 1.10"
current="$(terraform version -json 2>/dev/null | python3 -c 'import json,sys; print(json.load(sys.stdin)["terraform_version"])' 2>/dev/null || echo 0)"
if python3 -c "import sys; v=tuple(map(int,'$current'.split('.')[:2])); sys.exit(0 if v>=(1,10) else 1)" 2>/dev/null; then
  echo "Terraform $current"
else
  echo "Instalando Terraform $TF_VERSION en $TOOLS_DIR/bin (había: $current)"
  mkdir -p "$TOOLS_DIR/bin"
  tmp="$(mktemp -d)"
  curl -fL --retry 3 -o "$tmp/tf.zip" "https://releases.hashicorp.com/terraform/${TF_VERSION}/terraform_${TF_VERSION}_linux_amd64.zip"
  python3 -c "import zipfile,sys; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])" "$tmp/tf.zip" "$TOOLS_DIR/bin"
  chmod +x "$TOOLS_DIR/bin/terraform"
  rm -rf "$tmp"
  hash -r
  terraform version | head -1
fi

ACCOUNT="$(aws sts get-caller-identity --query Account --output text)" || fail "Sin credenciales del perfil $AWS_PROFILE."
echo "Cuenta: $ACCOUNT  Región: $AWS_REGION"

# ---------------------------------------------------------------------------
step "2/5 Bootstrap: bucket del estado, OIDC y rol de Terraform"
cd "$BOOT_DIR"
terraform init -input=false
OIDC_ARN="arn:aws:iam::${ACCOUNT}:oidc-provider/token.actions.githubusercontent.com"
if aws iam get-open-id-connect-provider --open-id-connect-provider-arn "$OIDC_ARN" >/dev/null 2>&1; then
  if ! terraform state list 2>/dev/null | grep -qx 'aws_iam_openid_connect_provider.github'; then
    echo "El proveedor OIDC ya existe: se importa."
    terraform import -input=false aws_iam_openid_connect_provider.github "$OIDC_ARN"
  fi
fi
# Correo para las alertas de AWS Budgets (50/80/100 %). Se puede pasar como BUDGET_ALERT_EMAIL=... bash ...
if [[ -z "${BUDGET_ALERT_EMAIL:-}" ]]; then
  read -r -p "Correo para alertas de gasto de AWS (cuenta del equipo): " BUDGET_ALERT_EMAIL
fi
export TF_VAR_budget_alert_email="$BUDGET_ALERT_EMAIL"
terraform apply -input=false -auto-approve
STATE_BUCKET="$(terraform output -raw state_bucket)"
TF_ROLE_ARN="$(terraform output -raw terraform_role_arn)"

# ---------------------------------------------------------------------------
step "3/5 Migrar el estado de deploy/terraform a s3://$STATE_BUCKET"
cd "$TF_DIR"
cat > backend.hcl <<HCL
bucket = "$STATE_BUCKET"
region = "$AWS_REGION"
HCL
terraform init -input=false -migrate-state -force-copy -backend-config=backend.hcl
if terraform state list | grep -qx 'aws_iam_openid_connect_provider.github\[0\]'; then
  echo "Sacando el proveedor OIDC del estado de la app (ahora vive en bootstrap)."
  terraform state rm 'aws_iam_openid_connect_provider.github[0]'
fi
if grep -q '^create_github_oidc_provider' terraform.tfvars; then
  sed -i 's/^create_github_oidc_provider.*/create_github_oidc_provider = false/' terraform.tfvars
else
  printf '\ncreate_github_oidc_provider = false\n' >> terraform.tfvars
fi
if [[ -f terraform.tfstate ]]; then
  mv terraform.tfstate "terraform.tfstate.local-backup-$(date +%Y%m%d%H%M%S)"
  echo "Respaldo del estado local guardado (ignorado por Git)."
fi

# ---------------------------------------------------------------------------
step "4/5 Plan de verificación (debe salir sin cambios o con cambios menores de IAM)"
SECRETS_FILE="$TF_DIR/.env.secrets"
[[ -f "$SECRETS_FILE" ]] || fail "Falta $SECRETS_FILE (MONGO_URI y JWT_SIGNING_KEY)."
set -a; . "$SECRETS_FILE"; set +a
export TF_VAR_mongo_connection_string="$MONGO_URI" TF_VAR_jwt_signing_key="$JWT_SIGNING_KEY"
terraform plan -input=false -out=tfplan
echo
read -r -p "¿Aplicar este plan ahora? (s/N) " answer
if [[ "${answer,,}" == "s" ]]; then
  terraform apply -input=false tfplan
fi
rm -f tfplan

# ---------------------------------------------------------------------------
step "5/5 Secretos de GitHub para infra.yml"
if command -v gh >/dev/null 2>&1 && gh auth status >/dev/null 2>&1; then
  printf '%s' "$TF_ROLE_ARN" | gh secret set AWS_TERRAFORM_ROLE_ARN -R "$GITHUB_REPO"
  printf '%s' "$MONGO_URI" | gh secret set MONGO_CONNECTION_STRING -R "$GITHUB_REPO"
  printf '%s' "$JWT_SIGNING_KEY" | gh secret set JWT_SIGNING_KEY -R "$GITHUB_REPO"
  gh variable set S3_BUCKET_NAME -R "$GITHUB_REPO" --body "$(grep -E '^s3_bucket_name' terraform.tfvars | cut -d'"' -f2)"
  echo "Secretos y variable configurados con gh."
else
  cat <<MSG
No encontré 'gh' autenticado. En GitHub -> Settings -> Secrets and variables -> Actions crea:
  Secret   AWS_TERRAFORM_ROLE_ARN   = $TF_ROLE_ARN
  Secret   MONGO_CONNECTION_STRING  = (el valor de MONGO_URI en deploy/terraform/.env.secrets)
  Secret   JWT_SIGNING_KEY          = (el valor de JWT_SIGNING_KEY en deploy/terraform/.env.secrets)
  Variable S3_BUCKET_NAME           = $(grep -E '^s3_bucket_name' terraform.tfvars | cut -d'"' -f2)
MSG
fi

cat <<MSG

Listo. Falta en GitHub (Settings -> Environments -> New environment):
  - Crea el environment "infra" y en "Required reviewers" ponte a ti (aprobación manual).
Después: Actions -> infra -> Run workflow -> plan / apply / destroy.
Desde ahora, para usar Terraform en local: terraform init -backend-config=backend.hcl
MSG
