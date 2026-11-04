using Microsoft.AspNetCore.Mvc;
using PosCaja.Application;

namespace Api.Redis.Controllers;

[ApiController]
[Route("api/ventas")]
public sealed class VentasController(IVentaService service) : ControllerBase
{
    [HttpGet("{idVenta:guid}/{idPos:int}")]
    [ProducesResponseType<VentaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VentaDto>> ObtenerPorId(Guid idVenta, int idPos, CancellationToken cancellationToken)
    {
        var venta = await service.ObtenerPorIdAsync(idVenta, idPos, cancellationToken);
        return venta is null ? NotFound() : Ok(venta);
    }

    [HttpGet]
    [ProducesResponseType<PaginaResultado<VentaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginaResultado<VentaDto>>> ObtenerPagina(
        CancellationToken cancellationToken, [FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = 50)
        => Ok(await service.ObtenerPaginaAsync(pagina, tamanoPagina, cancellationToken));
}
