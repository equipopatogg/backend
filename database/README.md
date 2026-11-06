# Script de la base de datos

`mongo-init.js` crea en MongoDB Atlas (M0, gratis) las colecciones de PactoGG con validadores de esquema y todos los índices. Es idempotente.

```bash
# Requiere mongosh: https://www.mongodb.com/try/download/shell
mongosh "mongodb+srv://USUARIO:CLAVE@cluster.xxxxx.mongodb.net/" database/mongo-init.js
```

- Base por defecto `pactogg`; otra con `PACTOGG_DB=nombre`.
- Los validadores están en modo `warn`: documentan el esquema y avisan en el log de Atlas, pero no rompen a la API.
- La API también crea los índices al arrancar (`MongoIndexInitializer`). El script sirve para entregar la BD lista y revisarla sin levantar la API.
- No contiene datos ni secretos. La cadena de conexión nunca se sube al repositorio.

| Colección | Para qué |
|---|---|
| `users` | Cuentas, apodo único, Google, estadísticas |
| `refresh_tokens` | Sesiones (refresh rotativo) |
| `groups` / `group_members` | Grupos privados, rol y saldo de puntos por grupo |
| `contracts` | Retos y su máquina de estados, jurados y votos contados |
| `evidence` | Fotos (S3) y enlaces aprobados por moderación |
| `votes` | Un voto inmutable por jurado |
| `points_ledger` | Libro de puntos solo de inserción (sin valor monetario) |
| `commitments` | Compromisos Debo / Me deben |
| `notifications` | Avisos dentro de la app (se borran solos a los 90 días) |
| `tournaments` / `matches` | Torneos y partidas |
| `audit_events` | Auditoría encadenada |
| `reports` | Reportes de contenido (pendiente) |
