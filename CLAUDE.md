# Guía y Principios de Desarrollo — ABA Backend

> **Este archivo es la fuente única de verdad para el flujo de trabajo de este repositorio.**
> Claude debe leerlo y aplicarlo antes de cualquier cambio.
> Es la contraparte de `CLAUDE.md` en el repositorio del frontend (`a.b.a-front`) — mismo equipo,
> mismas convenciones de Git; este archivo se enfoca en lo específico del backend.
>
> **Antes de empezar, `../a.b.a-front/docs/PENDIENTES.md`.** Los pendientes, decisiones y deuda de los dos
> repos viven ahí, igual que la planeación de OpenSpec. Lo que aparezca y no sea del cambio en curso se
> registra ahí (columna Repo = back) y no se corrige en ese cambio.

---

## Contexto del proyecto

- API para el punto de venta de **Alimentos Balanceados Alonso** (forrajería): productos, proveedores, compras
  (bultos y productos varios), ventas (bulto, kilo, pieza), inventario y gastos.
- Este repositorio (`backend-aba`, remoto `a.b.a-back`) es el **backend**. El frontend Angular vive por separado en
  el repo hermano `../a.b.a-front` (remoto `a.b.a-front`) y corre en
  `http://localhost:4200`, consumiendo esta API en `http://localhost:5028/api`. Son **repos de Git independientes**.
- Un cambio de contrato aquí (forma del JSON, nuevo endpoint, campo removido) casi siempre requiere el commit
  correspondiente en `a.b.a-front` — valida siempre contra `core/pos.models.ts` y `core/pos-api.service.ts` del
  frontend, no solo contra lo que parezca razonable desde el backend.
- La planeación de cambios con OpenSpec (`proposal.md`, `design.md`, `tasks.md`, `spec.md`) vive en el repo del
  frontend, bajo `openspec/changes/<id>/`, aunque el cambio toque este backend — no se duplica un `openspec/` aparte
  aquí.

## Stack del proyecto

- **ASP.NET Core 9** (Web API, controladores).
- **Entity Framework Core 9 + SQLite** (`aba.db`, generado localmente — no se versiona).
- **Migraciones de EF Core** para el esquema (`dotnet ef migrations add <Nombre>`, aplicadas con `Database.Migrate()`
  en `Program.cs`). No usar `EnsureCreated()` — no soporta cambios de esquema sobre una base ya existente.
- **Serilog** para logging (consola + archivo en `logs/`, no versionado).
- **Swashbuckle/Swagger** para explorar la API en `http://localhost:5028/swagger`.

## Flujo de trabajo Git

### Ramas base

- **`main`**: rama estable. No se trabaja directo sobre ella.
- **`dev`**: rama de integración. Toda rama de cambio se crea **a partir de `dev`**, nunca de `main`.
- Mismo modelo que en `a.b.a-front`.

### Nomenclatura de ramas

- Formato: `dev-<tipo>-<descripcion-en-kebab-case>`, creada desde `dev`.
- `<tipo>`: `feat`, `fix`, `chore`, `refactor`, `docs`, `test`, `perf`, `style`.
- Ejemplos: `dev-feat-alerta-stock-bajo`, `dev-fix-costo-por-pieza`.

**Comportamiento de Claude:** crea la rama él mismo desde `origin/dev`, con el mismo nombre que la rama del
front cuando el cambio toca los dos repos.

### Mensajes de commit

- Formato: `tipo(scope): descripción en minúscula`, **en inglés**.
- `scope`: área afectada (ej. `purchases`, `sales`, `inventory`, `expenses`, `suppliers`).
- Ejemplo: `feat(expenses): add expenses controller backed by ExpenseEntity`

**Comportamiento de Claude:** hace los commits él mismo cuando una unidad de trabajo queda completa y
`dotnet build` pasa. Cada commit hace una sola cosa.

### Pull Request

- Mismo formato que en `a.b.a-front`: título igual al commit, descripción en español con `## Resumen`,
  `## Cambios`, `## Pruebas`, `## Nota / deuda` (opcional).
- **Pregunta antes** de hacer push y de abrir el PR con `gh pr create` hacia `dev`, igual que en el front. Nunca
  fusiona un PR.
- La memoria del agente es solo para hechos de la máquina; lo del proyecto va al repositorio.

## Principios de desarrollo

- **Sin código huérfano**: si un campo/endpoint deja de usarse desde el frontend, se elimina aquí también (no dejarlo
  "por si acaso"). Ejemplo ya resuelto: `RealKg`/`RealKgTotal` se quitaron por completo del modelo.
- **Un lote = un costo**: el patrón de costeo por lote (`BagLotEntity` para bultos, `UnitLotEntity` para productos
  varios) es el único mecanismo de costo/ganancia del sistema — no introducir un camino alterno de costo promedio
  sin lote.
- **Nombres duplicados se resuelven, no se acumulan**: productos y proveedores se buscan por nombre (trim,
  case-insensitive) antes de crear uno nuevo, tanto en altas explícitas como en altas implícitas desde una compra.
- **No dejar migraciones a medias**: todo cambio de modelo va acompañado de su migración de EF Core en el mismo
  cambio; no se difiere para "después".

## Base de datos local

- `aba.db` es local y desechable — se regenera con `dotnet run` (aplica migraciones + siembra 3 productos de demo
  vía `PosStore.Seed`). No se versiona (ver `.gitignore`).
- Si cambias el modelo: genera la migración (`dotnet ef migrations add <Nombre>`), y si tu copia de `aba.db` quedó
  con un esquema viejo incompatible, bórrala y deja que la app la regenere.
