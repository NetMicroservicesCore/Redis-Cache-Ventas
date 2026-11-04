using Microsoft.AspNetCore.Mvc;
using PosCaja.Application;

namespace Api.Redis.Controllers;

[ApiController]
[Route("api/ventas")]
public sealed class VentasController(IVentaService service) : ControllerBase
{
    [HttpGet("{idVenta:guid}/{idPos:int}")]
    [EndpointSummary("Consultar una venta por su clave compuesta")]
    [EndpointDescription("Indica idVenta (UUID) e idPos (entero). Devuelve 404 si no existe esa combinación.")]
    [ProducesResponseType<VentaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<VentaDto>> ObtenerPorId(Guid idVenta, int idPos, CancellationToken cancellationToken)
    {
        var venta = await service.ObtenerPorIdAsync(idVenta, idPos, cancellationToken);
        return venta is null ? NotFound() : Ok(venta);
    }

    [HttpGet]
    [EndpointSummary("Consultar ventas paginadas")]
    [EndpointDescription("Página mínima 1; tamaño entre 1 y 100, predeterminado 50. Orden por idVenta e idPos. Devuelve items, pagina, tamanoPagina y totalRegistros. Una página sin resultados devuelve items vacío.")]
    [ProducesResponseType<PaginaResultado<VentaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PaginaResultado<VentaDto>>> ObtenerPagina(
        CancellationToken cancellationToken, [FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = 50)
        => Ok(await service.ObtenerPaginaAsync(pagina, tamanoPagina, cancellationToken));
}
