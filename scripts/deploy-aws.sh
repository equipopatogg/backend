#!/usr/bin/env bash
# Redespliega la imagen :latest de ECR en las dos instancias usando SSM Run Command.
# Uso: AWS_PROFILE=pactogg bash scripts/deploy-aws.sh
set -euo pipefail
export PATH="$HOME/.local/pactogg-tools/bin:/tmp/opencode/bin:/tmp/opencode/terraform:$PATH"
export AWS_PROFILE="${AWS_PROFILE:-pactogg}" AWS_PAGER=""

AWS_REGION="${AWS_REGION:-us-east-1}"
TF_DIR="$(cd "$(dirname "$0")/../deploy/terraform" && pwd)"
INSTANCE_IDS="${INSTANCE_IDS:-$(terraform -chdir="$TF_DIR" output -json instance_ids | python3 -c 'import json,sys; print(" ".join(json.load(sys.stdin)))')}"

# shellcheck disable=SC2086
command_id="$(aws ssm send-command \
  --region "$AWS_REGION" \
  --document-name AWS-RunShellScript \
  --comment "pactogg redeploy" \
  --instance-ids $INSTANCE_IDS \
  --parameters 'commands=["sudo /opt/pactogg/redeploy.sh"]' \
  --query Command.CommandId --output text)"

echo "SSM command: ${command_id}"

for id in $INSTANCE_IDS; do
  for _ in $(seq 1 60); do
    status="$(aws ssm get-command-invocation --region "$AWS_REGION" \
      --command-id "$command_id" --instance-id "$id" \
      --query Status --output text 2>/dev/null || echo Pending)"
    case "$status" in
      Success) echo "$id: OK"; break ;;
      Failed|Cancelled|TimedOut) echo "$id: $status" >&2; exit 1 ;;
    esac
    sleep 10
  done
  [[ "$status" == "Success" ]] || { echo "$id: timeout" >&2; exit 1; }
done
