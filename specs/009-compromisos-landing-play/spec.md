# 009 — Compromisos, landing y Google Play

**Fecha:** 2026-11-07 · **Estado:** Borrador para Kevin H. (dueño de la fila 101); parte de backend implementada salvo 009-T2 · **Sprint:** 4
**Historias:** ID 124 Compromisos por cumplir · ID 125 Compromisos a mi favor · ID 129 Recordatorios de compromisos · publicación en Google Play (pruebas internas, 16–27 nov).
**Endpoints del CRUD:** COM-01..05 (hechos), COM-06 interno (hecho).
**Fuentes:** constitución §6.6 y §12; CLARIFICACION #1, #89, #116, #131 y resumen (landing, Google Play, privacidad); Descripción del Backend 7.7; CRUD hoja Compromisos.

## 1. Contexto y objetivo

Al cumplirse un reto, la recompensa real (unos tacos) queda como compromiso con vistas Debo y Me deben. La app se distribuye por Google Play en pruebas internas. La landing page ya está hecha por Kevin H. fuera de este repositorio, así que no forma parte de esta spec.

## 2. Fuera de alcance

Pagos o canjes (regla de oro), tienda pública de Google Play, dominio de paga, publicidad y la landing page (ya terminada).

## 3. Requisitos funcionales (EARS)

### Compromisos (backend)

| ID | Requisito | Endpoint |
|---|---|---|
| 009-R1 | Cuando un reto termine `fulfilled`, el sistema deberá crear un compromiso del retador (deudor) hacia quien cumplió (acreedor) con la recompensa como descripción. | COM-06 |
| 009-R2 | Cuando un miembro pida sus compromisos, el sistema deberá filtrar por `role=debo` o `me-deben` y estado, con cursor; y devolver el detalle y el contador. | COM-01, COM-02, COM-05 |
| 009-R3 | Cuando el acreedor marque cumplido, el sistema deberá cerrarlo `completed`; cuando el deudor lo pida, deberá pasar a `completion_requested` y avisar al acreedor. | COM-03, COM-04 |
| 009-R4 | Cuando un compromiso lleve más de 7 días abierto, el sistema deberá avisar una vez al deudor (`commitment_overdue`) y contarlo para el Índice de Palabra. | Tarea programada (009-T2, igual que 006-T9) |

### Google Play (Kevin)

| ID | Requisito |
|---|---|
| 009-R8 | El equipo deberá publicar un AAB firmado en el canal de pruebas internas entre el 16 y el 27 de noviembre, con R8/ProGuard en release y sin claves en el APK. |
| 009-R9 | La ficha deberá enlazar el aviso de privacidad publicado en la landing y declarar los datos que se recogen (correo, apodo, fotos de evidencia, token de push). |

## 4. Criterios de aceptación

| ID | Requisito | Evidencia |
|---|---|---|
| 009-AC1 | R1–R3 | `JuryFlowTests`, `DetailEndpointsTests.CommitmentDetailAndSummary` |
| 009-AC2 | R4 | Por hacer (009-T2) |
| 009-AC4 | R8, R9 | Enlace de prueba interna que instala la app en un celular del equipo |

## 5. Preguntas abiertas

Tipo de cuenta de Google Play (personal u organización): pendiente desde la CLARIFICACION (resumen). Kevin la resuelve antes del 16 de noviembre.

## 6. Verificación contra la constitución

§1 regla de oro ✔ · §6.6 compromisos con vistas Debo / Me deben ✔ · §12 costo cero salvo Google Play ✔.

## Aprobación

| Rol | Persona | Fecha | Resultado |
|---|---|---|---|
| Dueño de la spec | Kevin H. | — | Por revisar |
| Product Owner | David M. | — | Por aprobar |

Expediente: [plan](plan.md), [tareas](tasks.md), [checklist](checklists/checklist.md).
