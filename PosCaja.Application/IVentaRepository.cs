namespace PosCaja.Application;

public interface IVentaRepository
{
    Task<VentaDto?> ObtenerPorIdAsync(Guid idVenta, int idPos, CancellationToken cancellationToken = default);
    Task<PaginaResultado<VentaDto>> ObtenerPaginaAsync(int pagina = 1, int tamanoPagina = 50, CancellationToken cancellationToken = default);
}
