using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PosCaja.Application;
using PosCaja.Domain;

namespace PosCaja.Infrastructure.SqlServer;

public sealed class VentaRepository(PosCajaDbContext context) : IVentaRepository
{
    private static readonly Expression<Func<Venta, VentaDto>> Projection = v => new VentaDto
    {
        IdVenta = v.IdVenta, IdPos = v.IdPos, Vendedor = v.Vendedor, Folio = v.Folio,
        FechaVenta = v.FechaVenta, TotalVendido = v.TotalVendido, PagoEfectivo = v.PagoEfectivo,
        PagoCheque = v.PagoCheque, PagoVales = v.PagoVales, PagoTc = v.PagoTc,
        Supervisor = v.Supervisor, Upload = v.Upload, NumRegistros = v.NumRegistros
    };

    public Task<VentaDto?> ObtenerPorIdAsync(Guid idVenta, int idPos, CancellationToken cancellationToken = default)
        => context.Ventas.AsNoTracking()
            .Where(v => v.IdVenta == idVenta && v.IdPos == idPos)
            .Select(Projection).SingleOrDefaultAsync(cancellationToken);

    public async Task<PaginaResultado<VentaDto>> ObtenerPaginaAsync(int pagina = 1, int tamanoPagina = 50, CancellationToken cancellationToken = default)
    {
        var query = context.Ventas.AsNoTracking();
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query.OrderBy(v => v.IdVenta).ThenBy(v => v.IdPos)
            .Skip(checked((pagina - 1) * tamanoPagina)).Take(tamanoPagina)
            .Select(Projection).ToListAsync(cancellationToken);
        return new(items, pagina, tamanoPagina, total);
    }
}
