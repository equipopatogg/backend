# Checklist — BE-01

[Spec](../spec.md) · [plan](../plan.md) · [tareas](../tasks.md).

| Criterio | Tareas | Estado | Evidencia / límite |
|---|---|---|---|
| BE01-AC1 | BE01-T1/T5 | PASS local / NO_EJECUTADO remoto | Build/test local; runner pendiente |
| BE01-AC2 | BE01-T1/T4 | PASS local / NO_EJECUTADO remoto | SCA local sin vulnerabilidades; SARIF remoto pendiente |
| BE01-AC3 | BE01-T1/T4 | PASS | `terraform fmt -check` y `validate` |
| BE01-AC4 | BE01-T2/T3/T5 | NO_EJECUTADO | Sin AWS/ECR |
| BE01-AC5 | BE01-T3/T5 | NO_EJECUTADO | Sin URL pública |

- [x] OIDC y permisos mínimos definidos.
- [x] Sin secretos en el workflow.
- [x] Validaciones locales ejecutadas.
- [ ] Runner, ECR, redespliegue SSM y smoke reales.
- [x] Límites declarados en validación.

