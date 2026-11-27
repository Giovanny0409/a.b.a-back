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
  reutilización de producto y proveedor, el tipo del producto (`bag`/`unit`) y las compras o ventas que no le
  corresponden, inventario, cotización y venta por bulto, kilo y pieza, ventas rechazadas, datos de la última compra,
  gastos, resumen, vendedores, siembra y la migración `AddProductKind` sobre datos del esquema anterior.
- `Api/`: la API completa con `WebApplicationFactory<Program>` sobre su propia base en memoria: rutas, códigos de
  respuesta y propiedades en camelCase que consume el front.

## Endpoints

- `GET /api/dashboard/summary`
- `GET /api/products` (cada producto con su `kind`: `bag` o `unit`)
- `GET /api/products/purchase-defaults` (por producto con compras: tipo, categoría, kg por bulto y precios de venta
  de su lote más reciente; para `unit`, `salePriceBag` es el precio de la pieza)
- `POST /api/products` (el producto queda `bag`)
- `GET /api/inventory`
- `GET /api/inventory/lots`
- `GET /api/purchases`
- `POST /api/purchases` (una o mas lineas `bag`/`unit`; da de alta producto y/o proveedor si no existen. La primera
  compra fija el tipo del producto; una línea con otro tipo responde `400 { message }`)
- `GET /api/sales`
- `GET /api/sales/quote` (`404` sin inventario; `400 { message }` si el modo no corresponde al tipo del producto,
  igual que `POST /api/sales/batch`)
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
