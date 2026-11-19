# Alarmas de CloudWatch (capa gratuita: 10 alarmas, métricas básicas de EC2 y filtros de logs).
# Avisan por SNS cuando una instancia falla, se satura la CPU o la API registra errores no controlados.

resource "aws_sns_topic" "alarms" {
  name              = "${local.name_prefix}-alarms"
  kms_master_key_id = "alias/aws/sns" # llave administrada por AWS: sin costo

  tags = { Name = "${local.name_prefix}-alarms" }
}

resource "aws_sns_topic_subscription" "alarm_email" {
  count     = var.alarm_email == "" ? 0 : 1
  topic_arn = aws_sns_topic.alarms.arn
  protocol  = "email"
  endpoint  = var.alarm_email
}

locals {
  monitored_instances = {
    gateway = aws_instance.gateway.id
    worker  = aws_instance.worker.id
  }
}

resource "aws_cloudwatch_metric_alarm" "instance_status" {
  for_each = local.monitored_instances

  alarm_name          = "${local.name_prefix}-${each.key}-status-check"
  alarm_description   = "La instancia ${each.key} no pasa las comprobaciones de estado de EC2."
  namespace           = "AWS/EC2"
  metric_name         = "StatusCheckFailed"
  dimensions          = { InstanceId = each.value }
  statistic           = "Maximum"
  period              = 300
  evaluation_periods  = 2
  threshold           = 1
  comparison_operator = "GreaterThanOrEqualToThreshold"
  treat_missing_data  = "breaching"
  alarm_actions       = [aws_sns_topic.alarms.arn]
  ok_actions          = [aws_sns_topic.alarms.arn]
}

resource "aws_cloudwatch_metric_alarm" "instance_cpu" {
  for_each = local.monitored_instances

  alarm_name          = "${local.name_prefix}-${each.key}-cpu-high"
  alarm_description   = "CPU de ${each.key} arriba de 85 % durante 15 minutos (t3.micro sin créditos se vuelve lenta)."
  namespace           = "AWS/EC2"
  metric_name         = "CPUUtilization"
  dimensions          = { InstanceId = each.value }
  statistic           = "Average"
  period              = 300
  evaluation_periods  = 3
  threshold           = 85
  comparison_operator = "GreaterThanThreshold"
  treat_missing_data  = "notBreaching"
  alarm_actions       = [aws_sns_topic.alarms.arn]
  ok_actions          = [aws_sns_topic.alarms.arn]
}

resource "aws_cloudwatch_metric_alarm" "cpu_credits" {
  for_each = local.monitored_instances

  alarm_name          = "${local.name_prefix}-${each.key}-cpu-credits-low"
  alarm_description   = "A ${each.key} le quedan pocos créditos de CPU; pronto bajará su rendimiento."
  namespace           = "AWS/EC2"
  metric_name         = "CPUCreditBalance"
  dimensions          = { InstanceId = each.value }
  statistic           = "Minimum"
  period              = 300
  evaluation_periods  = 3
  threshold           = 20
  comparison_operator = "LessThanThreshold"
  treat_missing_data  = "notBreaching"
  alarm_actions       = [aws_sns_topic.alarms.arn]
}

# Los errores no controlados que escribe ErrorHandlingMiddleware se vuelven una métrica.
resource "aws_cloudwatch_log_metric_filter" "api_errors" {
  name           = "${local.name_prefix}-api-unhandled-errors"
  log_group_name = aws_cloudwatch_log_group.api.name
  pattern        = "?\"Error no controlado\" ?\"Error de MongoDB\""

  metric_transformation {
    name          = "UnhandledErrors"
    namespace     = "PactoGG/Api"
    value         = "1"
    default_value = "0"
  }
}

resource "aws_cloudwatch_metric_alarm" "api_errors" {
  alarm_name          = "${local.name_prefix}-api-errors"
  alarm_description   = "La API registró 5 o más errores no controlados en 5 minutos (respuestas 500)."
  namespace           = "PactoGG/Api"
  metric_name         = aws_cloudwatch_log_metric_filter.api_errors.metric_transformation[0].name
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 5
  comparison_operator = "GreaterThanOrEqualToThreshold"
  treat_missing_data  = "notBreaching"
  alarm_actions       = [aws_sns_topic.alarms.arn]
  ok_actions          = [aws_sns_topic.alarms.arn]
}
