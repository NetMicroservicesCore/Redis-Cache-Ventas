# API de ventas — fases 1–3

API REST de solo lectura sobre la tabla existente `pos_caja.dbo.venta`, con .NET 10 y Entity Framework Core. Redis queda pendiente de la fase 4.

## Capas

- **PosCaja.Domain**: entidad Venta, sin dependencias externas.
- **PosCaja.Application**: DTO, contratos IVentaRepository/IVentaService, VentaService y validación.
- **PosCaja.Infrastructure.SqlServer**: DbContext, configuración de tabla y VentaRepository.
- **Api.Redis**: HTTP, OpenAPI, ProblemDetails y composición de dependencias.
- **PosCaja.Tests**: pruebas del servicio, metadatos EF y API mediante servidor de pruebas y repositorio simulado.

Flujo: `VentasController → IVentaService → VentaService → IVentaRepository → VentaRepository → SQL Server`.

Las bibliotecas se pueden referenciar desde otro host .NET 10. Para registrar el acceso a datos se utiliza `services.AddPosCajaSqlServer(options => configuration.GetSection(SqlServerOptions.SectionName).Bind(options))`; el host registra `IVentaService, VentaService` con alcance Scoped. La API carga la configuración y la biblioteca recibe opciones tipadas, sin leer archivos ni variables de entorno directamente y sin depender de HttpContext.

## Fase 1: configurar la conexión

Requisitos: SDK .NET 10 y acceso a SQL Server con permiso SELECT sobre dbo.venta. Ejecutar desde la carpeta de la solución:

```powershell
dotnet restore Api.Redis.slnx
dotnet user-secrets set "SqlServer:Server" "SERVIDOR\INSTANCIA" --project Api.Redis
dotnet user-secrets set "SqlServer:Database" "pos_caja" --project Api.Redis
dotnet user-secrets set "SqlServer:IntegratedSecurity" "true" --project Api.Redis
```

Sustituir servidor e instancia. La autenticación integrada utiliza la identidad del proceso y requiere que el sistema esté configurado para admitirla. Para autenticación SQL, establecer `SqlServer:IntegratedSecurity=false` y proporcionar `SqlServer:User` y `SqlServer:Password` en User Secrets. Si se cambia a autenticación integrada, eliminar ambas propiedades de credenciales. User Secrets es almacenamiento de desarrollo y no cifra los valores.

El certificado SQL debe ser válido para el servidor indicado; si el servidor local usa un certificado autofirmado, configurar su confianza o usar TrustServerCertificate=True exclusivamente para ese entorno de desarrollo.

### Configuración centralizada y producción

`SqlServerOptions` centraliza las propiedades y construye la cadena mediante `SqlConnectionStringBuilder`, que preserva contraseñas con caracteres especiales. `SqlServerOptionsValidator` valida al iniciar, incluso antes de la primera consulta, sin incluir los valores en sus mensajes de error. Nunca registrar las opciones completas ni la cadena generada.

| Variable de entorno | Predeterminado | Uso |
|---|---|---|
| `SqlServer__Server` | Sin valor | Obligatorio; servidor, instancia o host y puerto como `sql.example.com,1433`. |
| `SqlServer__Database` | Sin valor | Obligatorio; `pos_caja`. |
| `SqlServer__IntegratedSecurity` | `false` | `true` para autenticación integrada, `false` para usuario y contraseña SQL. |
| `SqlServer__User` | Sin valor | Obligatorio para autenticación SQL; omitir con autenticación integrada. |
| `SqlServer__Password` | Sin valor | Obligatorio para autenticación SQL; omitir con autenticación integrada. |
| `SqlServer__Encrypt` | `true` | Cifrar la conexión. |
| `SqlServer__TrustServerCertificate` | `false` | Validar el certificado del servidor. |

El proveedor estándar de .NET convierte `__` en `:`. Las variables de entorno tienen prioridad sobre appsettings y User Secrets; los argumentos de línea de comandos tienen prioridad sobre las variables de entorno. No pasar contraseñas como argumentos de ejecución.

Ejemplo de variables no secretas en PowerShell para un proceso de producción:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:SqlServer__Server = "sql.example.com,1433"
$env:SqlServer__Database = "pos_caja"
$env:SqlServer__IntegratedSecurity = "false"
$env:SqlServer__Encrypt = "true"
$env:SqlServer__TrustServerCertificate = "false"
```

Inyectar `SqlServer__User` y `SqlServer__Password` mediante el mecanismo de secretos del servicio, contenedor u orquestador. Las variables deben estar disponibles para el proceso que hospeda la API. Las opciones se leen al iniciar: reiniciar la aplicación después de cambiar sus valores.

La validación comprueba la configuración obligatoria, no la conectividad ni la validez de las credenciales en SQL Server. Un inicio correcto no sustituye la prueba de conexión.

**Migración:** `ConnectionStrings:PosCaja` y `ConnectionStrings__PosCaja` ya no se utilizan. Reemplazarlas por las propiedades anteriores y retirar los valores antiguos del entorno o User Secrets. No se migran secretos automáticamente.

No se ejecutan migraciones, EnsureCreated, Migrate ni el script CREATE TABLE. Las claves foráneas se representan como valores escalares; no se consultan las tablas relacionadas.

## Fase 2: comportamiento de consultas

La clave de venta es compuesta: IdVenta (Guid) e IdPos (int). Los importes conservan la precisión del script y Supervisor permite null. FechaVenta se devuelve sin asignar zona horaria.

Las lecturas usan AsNoTracking y proyección a DTO. El listado se ordena por id_venta y después id_pos y pagina dentro de SQL Server. El total utiliza LongCountAsync. El conteo y la página son consultas separadas: modificaciones concurrentes pueden hacer que reflejen instantes distintos. La paginación por desplazamiento también puede cambiar entre peticiones si se insertan o eliminan ventas.

## Fase 3: ejecutar y consultar

```powershell
dotnet build Api.Redis.slnx
dotnet test Api.Redis.slnx
dotnet run --project Api.Redis --launch-profile https
```

API: https://localhost:7096. Si el certificado HTTPS local no es de confianza, ejecutar `dotnet dev-certs https --trust`.

- `GET /api/ventas/{idVenta}/{idPos}`: 200 o 404.
- `GET /api/ventas?pagina=1&tamanoPagina=50`: página con items, pagina, tamanoPagina y totalRegistros.
- `GET /openapi/v1.json`: documento OpenAPI en Development.

El archivo Api.Redis/Api.Redis.http incluye ejemplos. Sustituir los identificadores del ejemplo por claves reales.

Página mínima: 1. Tamaño: 1 a 100, predeterminado 50. La paginación inválida o un desplazamiento superior a Int32.MaxValue devuelve 400. Una página vacía devuelve 200 e items vacío. Los errores internos devuelven ProblemDetails genérico y un traceId; no se convierten en páginas vacías.

Las pruebas automatizadas no requieren SQL Server ni Redis. Comprueban validación, claves, cancelación, HTTP, serialización, metadatos de mapeo y protección de errores. **No sustituyen una prueba contra SQL Server real.**

Para validar la integración real, configurar la conexión y comprobar:

1. Consultar una clave existente y comparar todas sus columnas, importes y nulls con SQL.
2. Consultar una clave inexistente y verificar 404; verificar ambos IdPos si existen ventas con el mismo IdVenta.
3. Recorrer páginas sobre datos sin cambios concurrentes; comparar orden, cantidad y total con SQL.
4. Consultar una página fuera del total y verificar items vacío.

El Dockerfile incluye las bibliotecas necesarias para compilar la API. El despliegue y la ejecución en contenedor no forman parte de estas fases.

## Pendiente: fase 4

Crear PosCaja.Infrastructure.Redis con CachedVentaRepository e IDistributedCache, expiración absoluta de 60 segundos y retorno a SQL ante fallos de Redis. La integración se hará después de la confirmación del usuario.

La API actual es para desarrollo local, sin autenticación. No se debe publicar como servicio compartido sin completar esa etapa.
