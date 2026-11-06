# Plan — 003 Grupos

[Spec](spec.md).

## Enfoque

Grupo + membresía como colecciones separadas (saldo por miembro); operaciones de cupo atómicas en Mongo; caché por versiones en Redis.

## Archivos

| Ruta | Cambio |
|---|---|
| `Pactogg.Api/Modules/Groups/*` | Crear, listar, detalle, unirse, miembros |
| `Pactogg.Api/Modules/Points/*` | Saldo, movimientos y ranking |
| `Pactogg.Api/Infrastructure/Mongo/Repositories/MongoGroup*.cs` | Persistencia e incrementos atómicos |

## Pruebas

Integración (crear, unirse, lleno, ajeno), seguridad (401).

## Orden

1. Crear y código. 2. Unirse. 3. Listados. 4. Puntos. 5. Documentación.
