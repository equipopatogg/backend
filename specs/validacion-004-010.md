# Validación SDD — 004 a 010

**Fecha:** 2026-11-18 · **Filas del Sprint Planning:** 105 (004–006) y 149 (007–010) · **Responsable:** David M. (PO)
**Criterio:** CLARIFICACION #138 (spec completa) y #139 (funcionalidad aprobada); constitución §9.3 y §11. Mismo formato que `validacion-001-003.md`.

## 1. Spec completa (#138)

| Criterio | 004 Contratos | 005 Voz | 006 Push | 007 Evidencias | 008 Jurado | 009 Compromisos/Play | 010 Torneos |
|---|---|---|---|---|---|---|---|
| Contexto | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| Fuera de alcance | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| Requisitos EARS | PASS (R1–R16) | PASS (R1–R15) | PASS (R1–R15) | PASS (R1–R11) | PASS (R1–R12) | PASS (R1–R4, R8, R9) | PASS (R1–R16) |
| Requisitos no funcionales | PASS | PASS | PASS | PASS | PASS | FAIL (sin sección) | PASS |
| Endpoints con IDs del Excel | PASS | PASS (CON-08) | PASS | PASS | PASS | FAIL (sin sección; IDs solo en R) | PASS |
| Criterios de aceptación por requisito | PASS (17 AC) | PASS (13 AC) | PASS (13 AC) | PASS (10 AC + AC9b) | PASS (9 AC) | FAIL (AC3 no existe; R8–R9 manual) | FAIL → PASS (R8 sin AC; se agregó 010-AC9) |
| Cero preguntas abiertas | PASS | FAIL → PASS (Q-007 cerrada, F2-D78) | PASS | PASS | PASS | FAIL (tipo de cuenta de Google Play) | PASS |
| Verificación contra la constitución | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| Aprobación del PO | PASS (11-06) | PASS (11-06) | PASS (11-06) | PASS (11-07) | PASS (11-07) | FAIL (por aprobar) | PASS (11-07) |

## 2. Funcionalidad aprobada (#139)

| Criterio | 004 | 005 | 006 | 007 | 008 | 009 | 010 |
|---|---|---|---|---|---|---|---|
| Criterios cumplidos | PASS (004-T9 demo hecha en la app, PL-07) | PASS (Groq en producción, probado desde la app el 11-18) | PASS backend; push real pendiente de 006-T7 | PASS | PASS | PASS backend; R8–R9 pendientes (Play) | PASS; prueba manual con 2 grupos pendiente |
| Pruebas en verde | PASS | PASS | PASS | PASS (incluye `TextModerationPolicyTests`) | PASS | PASS | PASS |
| Escaneos sin hallazgos críticos | PASS (CI del PR #34 en verde; confirmar el run de `main`) | PASS | PASS | PASS | PASS | PASS | PASS |
| Existe validación | PASS (este documento) | PASS | PASS | PASS | PASS | PASS | PASS |
| Revisó otra persona | PENDIENTE (Kevin, José) | PENDIENTE | PENDIENTE | PENDIENTE | PENDIENTE | PENDIENTE | PENDIENTE |
| Aprobó el PO | PASS | PASS | PASS | PASS | PASS | FAIL | PASS |

## 3. Cambios hechos al validar

| Hallazgo | Spec | Corrección |
|---|---|---|
| Q-007 seguía abierta aunque la IA ya funciona | 005 | Cerrada con F2-D78 (Groq, `openai/gpt-oss-20b`); 005-T9 marcada |
| 004-T9 (demo) seguía abierta | 004 | Marcada: demo de 6 pasos en la app (PL-07) |
| Parte móvil de 006 (T11–T13) sin cerrar | 006 | Marcadas: PL-10 push y permisos, PL-12 lectura sin conexión. Cambio de alcance: las acciones no se encolan sin conexión para no repetir cobros de fianza ni votos |
| 010-R8 sin criterio de aceptación aunque tenía prueba | 010 | 010-AC9 → `Spec010MatchTests.R8_Invite_OnlyNotifiesAdminsOfGroupsWhereTheOrganizerIsMember` |
| "kills" se rechazaba por Comprehend | 007 | Enmienda de 007-R11 y 007-AC9b (F2-D76) |
| Foto de perfil pedida por el PO | USR-07 | Spec nueva (F2-D77) |

## 4. Pendientes para cerrar del todo

1. 009: Kevin completa RNF, sección de endpoints, AC3, decide el tipo de cuenta de Google Play y el PO la aprueba.
2. 006-T7: Firebase en SSM (`scripts/configure-firebase.sh`) y redespliegue.
3. 005-T10: registrar la prueba con 20 frases.
4. Revisión técnica de Kevin y de comprobabilidad de José en las siete specs.
5. Pruebas manuales en teléfono con varias cuentas (torneos con 2 grupos, jurado con 3 cuentas).
