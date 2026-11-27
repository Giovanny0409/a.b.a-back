# Backend A.B.A

API para el punto de venta de Alimentos Balanceados Alonso: productos, proveedores, compras (bultos y productos
varios), ventas (bulto, kilo, pieza), inventario y gastos. Contraparte del frontend Angular en `a.b.a-front`.

Los datos se guardan en SQLite con Entity Framework Core. Al iniciar el backend se aplican las migraciones de EF
Core (creando `aba.db` si todavia no existe) y se siembran 3 productos de demo si la base esta vacia.

## Ejecutar

```powershell
dotnet restore
dotnet run
```

Swagger:

```text
http://localhost:5028/swagger
```

## Pruebas

```powershell
dotnet test
```

Corre `tests/BackendAba.Tests` (xUnit, en `backend-aba.sln`) sin tocar `aba.db`:

- `Services/`: `PosStore` contra SQLite en memoria con las migraciones reales: compras de bultos y piezas,
  reutilización de producto y proveedor, inventario, cotización y venta por bulto, kilo y pieza, ventas rechazadas,
  gastos, resumen, vendedores y siembra.
- `Api/`: la API completa con `WebApplicationFactory<Program>` sobre su propia base en memoria: rutas, códigos de
  respuesta y propiedades en camelCase que consume el front.

## Endpoints

- `GET /api/dashboard/summary`
- `GET /api/products`
- `POST /api/products`
- `GET /api/inventory`
- `GET /api/inventory/lots`
- `GET /api/purchases`
- `POST /api/purchases` (una o mas lineas `bag`/`unit`; da de alta producto y/o proveedor si no existen)
- `GET /api/sales`
- `GET /api/sales/quote`
- `POST /api/sales`
- `POST /api/sales/batch`
- `GET /api/expenses`
- `POST /api/expenses`
- `POST /api/sellers/authorize`
- `GET /api/history/sales`
- `GET /api/history/purchases`

## Base de datos

La cadena de conexion esta en `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=aba.db"
}
```

El esquema se gestiona con migraciones de EF Core (`Migrations/`), no con `EnsureCreated()` — un cambio de modelo
siempre debe venir acompañado de su propia migracion (`dotnet ef migrations add <Nombre>`). `aba.db` es local y
desechable, no se versiona.
