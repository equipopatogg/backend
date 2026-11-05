# Cómo contribuir a PactoGG Backend

## Ramas

- `main` siempre está estable y desplegada: **nadie hace push directo a `main`**.
- Cada cambio va en su rama, creada desde `main` actualizada:
  - `feature/<tema>` para funcionalidad nueva (ej. `feature/jurado-y-puntos`)
  - `fix/<tema>` para correcciones
  - `docs/<tema>` para documentación
  - `chore/<tema>` para configuración, dependencias o pipeline

## Flujo

```bash
git checkout main
git pull
git checkout -b feature/mi-cambio
# ... cambios ...
dotnet test -c Release
git add <archivos>
git commit -m "feat: descripción corta en presente"
git push -u origin feature/mi-cambio
```

Después se abre un **Pull Request** hacia `main` en GitHub. El pipeline `backend-ci-cd` corre en el PR (build, pruebas, CodeQL, Trivy, Terraform) y **no despliega**. Cuando todo está en verde se fusiona; el despliegue a AWS ocurre solo al fusionar en `main`.

## Mensajes de commit

Formato [Conventional Commits](https://www.conventionalcommits.org/es/): `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`, `ci:`.

## Antes de abrir el PR

- `dotnet test -c Release` en verde.
- `api/openapi.yaml`, la spec en `specs/` y `docs/API-movil.md` actualizados si cambió un endpoint.
- Nunca subir secretos: `session-*.md`, `*.tfvars`, `.env.secrets`, `appsettings.*.local.json`, llaves de AWS.
