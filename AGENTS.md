# Guía del repositorio

## Estructura del proyecto y organización de módulos

La solución utiliza .NET 10 y ofrece consultas de solo lectura sobre `pos_caja.dbo.venta` en SQL Server.

- `Api.Redis/`: controladores, registro de dependencias, configuración, ProblemDetails, OpenAPI y Swagger UI. Los ejemplos están en `Api.Redis.http`; los recursos de Swagger provienen de su paquete NuGet.
- `PosCaja.Domain/`: entidades sin dependencias de infraestructura.
- `PosCaja.Application/`: DTO, contratos de repositorios y servicios, validación de paginación y servicios.
- `PosCaja.Infrastructure.SqlServer/`: mapeos de EF Core, repositorio y configuración tipada de SQL.
- `PosCaja.Tests/`: pruebas automatizadas.

Mantén HTTP en la API y el acceso a datos en infraestructura. La integración de Redis está pendiente.

## Comandos de compilación, pruebas y desarrollo

Ejecuta desde la raíz con el SDK de .NET 10:

- `dotnet restore Api.Redis.slnx`: restaura dependencias NuGet.
- `dotnet build Api.Redis.slnx`: compila todos los proyectos.
- `dotnet test Api.Redis.slnx`: ejecuta las pruebas.
- `dotnet test Api.Redis.slnx --collect:"XPlat Code Coverage"`: recopila cobertura.
- `dotnet run --project Api.Redis --launch-profile https`: inicia localmente; abre `https://localhost:7096/swagger`.

## Estilo de código y convenciones de nombres

Usa sangría de cuatro espacios, espacios de nombres con ámbito de archivo, referencias anulables y modificadores de acceso explícitos. Utiliza PascalCase para tipos y propiedades, camelCase para parámetros, el prefijo `I` para interfaces y el sufijo `Async` para métodos asíncronos. Conserva nombres como `Venta` y `ObtenerPaginaAsync`. Propaga los tokens de cancelación. No hay configuración específica de formateadores ni linters.

## Directrices de pruebas

Usa xUnit y WebApplicationFactory; las pruebas HTTP sustituyen el repositorio SQL por una implementación de prueba. Nombra las clases `*Tests` y los métodos descriptivamente, como `InvalidPaginationDoesNotReachRepository`.

Verifica cambios de comportamiento, configuración, claves compuestas, precisión decimal y errores HTTP. No existe un porcentaje mínimo de cobertura. Las pruebas automatizadas no requieren SQL Server ni Redis; informa por separado las verificaciones con datos reales.

## Directrices para commits y pull requests

El historial usa mensajes descriptivos, principalmente en español; no exige Conventional Commits. Mantén cada commit enfocado. Las PR deben explicar los cambios, vincular incidencias relevantes, indicar resultados de pruebas y señalar cambios de configuración o verificaciones pendientes. Incluye capturas solo para cambios visuales.

## Seguridad y configuración

Usa variables `SqlServer__*` o User Secrets en desarrollo. Nunca incluyas credenciales en commits ni registres cadenas de conexión. Los perfiles locales omiten validar certificados; producción requiere cifrado y un certificado confiable. Conserva la clave compuesta `(IdVenta, IdPos)`. No ejecutes migraciones ni métodos de creación de esquemas sobre la base existente.
