#!/usr/bin/env bash
# Primer despliegue de PactoGG en AWS desde WSL.
#
#   cd /mnt/g/PactoGG && bash scripts/deploy-first-time.sh
#
# Pasos: herramientas -> perfil AWS -> comprobación de permisos -> secretos locales ->
# terraform init/validate -> ECR -> build/push de la imagen -> plan -> apply -> /health.
#
# Arquitectura de costo cero (cubierta por créditos): 2 EC2 t3.micro, Caddy como
# balanceador + TLS, Redis en contenedor, S3 + CloudFront, CloudWatch Logs.
# Sin NAT Gateway, sin ALB, sin ElastiCache.
#
# Variables opcionales:
#   AWS_PROFILE        (pactogg)          AWS_REGION   (us-east-1)
#   AWS_KEYS_CSV       (/mnt/c/Users/david/Downloads/PactoGGadmin_accessKeys.csv)
#   S3_BUCKET_NAME     (pactogg-evidence-bucket-us-east-1)
#   DOMAIN_NAME        vacío => HTTPS gratis con <ip>.sslip.io + Let's Encrypt
#   MONGO_URI          si no está, se lee de deploy/terraform/.env.secrets o se pide oculto
#   AUTO_APPROVE=1     aplica sin pedir confirmación
#
# Ningún secreto se imprime ni se escribe en archivos versionados.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TF_DIR="$REPO_ROOT/deploy/terraform"
TOOLS_DIR="${TOOLS_DIR:-$HOME/.local/pactogg-tools}"
TF_VERSION="1.10.5"

export AWS_PROFILE="${AWS_PROFILE:-pactogg}"
export AWS_REGION="${AWS_REGION:-us-east-1}"
export AWS_DEFAULT_REGION="$AWS_REGION"
export AWS_PAGER=""
AWS_KEYS_CSV="${AWS_KEYS_CSV:-/mnt/c/Users/david/Downloads/PactoGGadmin_accessKeys.csv}"
S3_BUCKET_NAME="${S3_BUCKET_NAME:-pactogg-evidence-bucket-us-east-1}"
DOMAIN_NAME="${DOMAIN_NAME:-}"
SECRETS_FILE="$TF_DIR/.env.secrets"

step() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }
fail() { printf '\n\033[1;31mERROR:\033[0m %s\n' "$*" >&2; exit 1; }

confirm() {
  [[ "${AUTO_APPROVE:-0}" == "1" ]] && return 0
  read -r -p "$1 [s/N] " answer
  [[ "$answer" =~ ^[sSyY]$ ]]
}

# ---------------------------------------------------------------------------
step "1/9 Herramientas (aws, terraform, docker)"
mkdir -p "$TOOLS_DIR/bin"
export PATH="$TOOLS_DIR/bin:/tmp/opencode/bin:/tmp/opencode/terraform:$PATH"

unzip_py() { python3 -c 'import sys, zipfile; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])' "$1" "$2"; }

if ! command -v aws >/dev/null 2>&1; then
  echo "Instalando AWS CLI v2 en $TOOLS_DIR ..."
  tmp="$(mktemp -d)"
  curl -fL --retry 3 -o "$tmp/awscli.zip" https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip
  unzip_py "$tmp/awscli.zip" "$tmp"
  chmod -R u+x "$tmp/aws"
  "$tmp/aws/install" --install-dir "$TOOLS_DIR/aws-cli" --bin-dir "$TOOLS_DIR/bin" --update
  rm -rf "$tmp"
fi

if ! command -v terraform >/dev/null 2>&1; then
  echo "Instalando Terraform $TF_VERSION en $TOOLS_DIR ..."
  tmp="$(mktemp -d)"
  curl -fL --retry 3 -o "$tmp/tf.zip" "https://releases.hashicorp.com/terraform/${TF_VERSION}/terraform_${TF_VERSION}_linux_amd64.zip"
  unzip_py "$tmp/tf.zip" "$TOOLS_DIR/bin"
  chmod +x "$TOOLS_DIR/bin/terraform"
  rm -rf "$tmp"
fi

DOCKER="docker"
DOCKER_IS_WINDOWS=0
if ! docker info >/dev/null 2>&1; then
  DOCKER="/mnt/c/Program Files/Docker/Docker/resources/bin/docker.exe"
  DOCKER_IS_WINDOWS=1
  "$DOCKER" info >/dev/null 2>&1 || fail "Docker no responde. Abre Docker Desktop y vuelve a ejecutar."
fi
aws --version
terraform version | head -1
"$DOCKER" --version

# ---------------------------------------------------------------------------
step "2/9 Perfil AWS '$AWS_PROFILE'"
if ! aws configure list-profiles 2>/dev/null | grep -qx "$AWS_PROFILE"; then
  [[ -f "$AWS_KEYS_CSV" ]] || fail "No existe el perfil $AWS_PROFILE ni el CSV $AWS_KEYS_CSV."
  python3 - "$AWS_KEYS_CSV" "$AWS_PROFILE" "$AWS_REGION" <<'PY'
import configparser, csv, os, sys
from pathlib import Path
src, profile, region = sys.argv[1:4]
with open(src, newline="", encoding="utf-8-sig") as f:
    row = next(csv.DictReader(f))
aws_dir = Path.home() / ".aws"
aws_dir.mkdir(mode=0o700, exist_ok=True)
creds, conf = aws_dir / "credentials", aws_dir / "config"
c = configparser.RawConfigParser(); c.read(creds)
c[profile] = {"aws_access_key_id": row["Access key ID"].strip(),
              "aws_secret_access_key": row["Secret access key"].strip()}
with open(creds, "w") as f: c.write(f)
os.chmod(creds, 0o600)
k = configparser.RawConfigParser(); k.read(conf)
k[f"profile {profile}"] = {"region": region, "output": "json"}
with open(conf, "w") as f: k.write(f)
print(f"perfil {profile} configurado (sin mostrar claves)")
PY
fi
aws sts get-caller-identity --output table

# ---------------------------------------------------------------------------
step "3/9 Permisos de despliegue"
missing=()
check() {
  local name="$1"; shift
  local out
  if ! out="$("$@" 2>&1 >/dev/null)"; then
    missing+=("$name")
    printf '  \033[1;31m%-12s\033[0m %s\n' "$name" "$(printf '%s' "$out" | grep -m1 -E 'error|Error|denied|Denied|Opt|Pending|Blocked|not' || printf '%s' "$out" | head -1)"
  else
    printf '  %-12s OK\n' "$name"
  fi
}
check "EC2/VPC"     aws ec2 describe-vpcs --max-items 1
check "IAM"         aws iam list-roles --max-items 1
check "S3"          aws s3api list-buckets
check "ECR"         aws ecr describe-repositories --max-items 1
check "SSM"         aws ssm describe-parameters --max-items 1
check "CloudFront"  aws cloudfront list-distributions --max-items 1
if ((${#missing[@]})); then
  user_arn="$(aws sts get-caller-identity --query Arn --output text)"
  fail "Falla(n): ${missing[*]} para $user_arn (mensaje real arriba).
- AccessDenied/UnauthorizedOperation: falta AdministratorAccess o los permisos aún se propagan (espera 2-3 min).
- OptInRequired/PendingVerification/Blocked: la cuenta AWS aún no está verificada o el plan gratuito no habilita el servicio."
fi
echo "Permisos OK."

# ---------------------------------------------------------------------------
step "4/9 Secretos locales (no versionados)"
umask 077
[[ -f "$SECRETS_FILE" ]] && set -a && . "$SECRETS_FILE" && set +a
if [[ -z "${MONGO_URI:-}" ]]; then
  read -r -s -p "Cadena de conexión de MongoDB Atlas (mongodb+srv://usuario:contraseña@...): " MONGO_URI; echo
fi
[[ "$MONGO_URI" == mongodb* ]] || fail "La cadena de MongoDB no empieza por mongodb:// o mongodb+srv://"
[[ "$MONGO_URI" == *"<db_password>"* ]] && fail "Sustituye <db_password> por la contraseña real."
[[ "$MONGO_URI" == *"?"* ]] || MONGO_URI="${MONGO_URI%/}/?retryWrites=true&w=majority&appName=pactogg"
JWT_SIGNING_KEY="${JWT_SIGNING_KEY:-$(python3 -c 'import secrets; print(secrets.token_urlsafe(48))')}"
printf 'MONGO_URI=%q\nJWT_SIGNING_KEY=%q\n' "$MONGO_URI" "$JWT_SIGNING_KEY" > "$SECRETS_FILE"
chmod 600 "$SECRETS_FILE"
export TF_VAR_mongo_connection_string="$MONGO_URI"
export TF_VAR_jwt_signing_key="$JWT_SIGNING_KEY"
echo "Secretos en $SECRETS_FILE (ignorado por Git)."

if [[ ! -f "$TF_DIR/terraform.tfvars" ]]; then
  cat > "$TF_DIR/terraform.tfvars" <<EOF
aws_region     = "$AWS_REGION"
project_name   = "pactogg"
environment    = "prod"
instance_type  = "t3.micro"
domain_name    = "$DOMAIN_NAME"
s3_bucket_name = "$S3_BUCKET_NAME"

github_repo        = "equipopatogg/backend"
github_branch      = "main"
github_environment = "production"
EOF
  echo "Creado deploy/terraform/terraform.tfvars (sin secretos)."
fi

# ---------------------------------------------------------------------------
step "5/9 terraform init / fmt / validate"
cd "$TF_DIR"
if [[ ! -f backend.hcl ]]; then
  # Estado remoto en S3 (necesario para crear/destruir desde el pipeline).
  bash "$REPO_ROOT/scripts/bootstrap-terraform-pipeline.sh"
fi
terraform init -input=false -upgrade=false -backend-config=backend.hcl
terraform fmt -recursive
terraform validate

# ---------------------------------------------------------------------------
step "6/9 Repositorio ECR"
terraform apply -input=false -auto-approve -target=aws_ecr_repository.api -target=aws_ecr_lifecycle_policy.api
ECR_URL="$(terraform output -raw ecr_repository_url 2>/dev/null || aws ecr describe-repositories --repository-names pactogg-api --query 'repositories[0].repositoryUri' --output text)"
ECR_REGISTRY="${ECR_URL%%/*}"
echo "ECR: $ECR_URL"

# ---------------------------------------------------------------------------
step "7/9 Build y push de la imagen"
cd "$REPO_ROOT"
TAG="$(date +%Y%m%d%H%M%S)"
CONTEXT="$REPO_ROOT"
[[ "$DOCKER_IS_WINDOWS" == "1" ]] && CONTEXT="$(wslpath -w "$REPO_ROOT")"
aws ecr get-login-password | "$DOCKER" login --username AWS --password-stdin "$ECR_REGISTRY"
"$DOCKER" build -t "$ECR_URL:latest" -t "$ECR_URL:$TAG" "$CONTEXT"
"$DOCKER" push "$ECR_URL:$TAG"
"$DOCKER" push "$ECR_URL:latest"

# ---------------------------------------------------------------------------
step "8/9 terraform plan / apply"
cd "$TF_DIR"
terraform plan -input=false -out=tfplan
confirm "¿Aplicar este plan en AWS? (VPC, 2 EC2 t3.micro, 2 Elastic IP, S3, CloudFront, CloudWatch; sin NAT/ALB/ElastiCache)" || fail "Cancelado por el usuario. El plan quedó en deploy/terraform/tfplan."
terraform apply -input=false tfplan
rm -f tfplan

API_URL="$(terraform output -raw api_url)"
HEALTH_URL="$(terraform output -raw health_check_url)"
GW_IP="$(terraform output -raw gateway_public_ip)"
WK_IP="$(terraform output -raw worker_public_ip)"
CDN_URL="$(terraform output -raw cdn_base_url)"

# ---------------------------------------------------------------------------
step "9/9 Smoke test $HEALTH_URL"
echo "Las instancias instalan Docker, bajan la imagen y Caddy pide el certificado: 5-10 min."
for i in $(seq 1 60); do
  code="$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "$HEALTH_URL" || true)"
  echo "  intento $i: HTTP $code"
  [[ "$code" == "200" ]] && break
  sleep 20
done
[[ "$code" == "200" ]] || fail "/health no respondió 200. Revisa CloudWatch ($(terraform output -raw log_group)) o entra por SSM: aws ssm start-session --target <instance_id>"

cat <<EOF

=========================================================
 PactoGG desplegado
   API:     $API_URL
   Health:  $HEALTH_URL
   CDN:     $CDN_URL
   Swagger: no se expone en Production

 PENDIENTE en MongoDB Atlas -> Network Access, agrega:
   $GW_IP/32   (instancia 1)
   $WK_IP/32   (instancia 2)
   (o deja 0.0.0.0/0 temporalmente). Sin esto /health responde,
   pero los endpoints que usan Mongo fallan.

 Para apagar todo y no gastar créditos:  bash deploy/terraform/destroy.sh
=========================================================
EOF
