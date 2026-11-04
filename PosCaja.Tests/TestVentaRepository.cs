using PosCaja.Application;

namespace PosCaja.Tests;

public sealed class TestVentaRepository : IVentaRepository
{
    public static readonly Guid VentaId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public int Calls { get; private set; }
    public CancellationToken LastToken { get; private set; }
    public bool Fail { get; set; }
    public Task<VentaDto?> ObtenerPorIdAsync(Guid idVenta, int idPos, CancellationToken cancellationToken = default)
    {
        Calls++;
        LastToken = cancellationToken;
        if (Fail) throw new InvalidOperationException("Server=private;Password=secret");
        return Task.FromResult(idVenta == VentaId && idPos is 1 or 2 ? new VentaDto
        {
            IdVenta = idVenta, IdPos = idPos, Vendedor = "cajero", Folio = 123,
            FechaVenta = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Unspecified),
            TotalVendido = 123.456m, PagoCheque = 12.345m, PagoEfectivo = 11.22m
        } : null);
    }
    public Task<PaginaResultado<VentaDto>> ObtenerPaginaAsync(int pagina = 1, int tamanoPagina = 50, CancellationToken cancellationToken = default)
    {
        Calls++;
        LastToken = cancellationToken;
        if (Fail) throw new InvalidOperationException("Server=private;Password=secret");
        return Task.FromResult(new PaginaResultado<VentaDto>([], pagina, tamanoPagina, 0));
    }
}
