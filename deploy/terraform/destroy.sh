#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

if [[ ! -f terraform.tfvars ]]; then
  echo "Falta deploy/terraform/terraform.tfvars." >&2
  exit 1
fi

BUCKET="$(terraform output -raw evidence_bucket 2>/dev/null || true)"
if [[ -n "${BUCKET}" ]]; then
  echo "Vaciando bucket ${BUCKET} antes de destruir."
  aws s3 rm "s3://${BUCKET}" --recursive || true
fi

terraform destroy -auto-approve "$@"
