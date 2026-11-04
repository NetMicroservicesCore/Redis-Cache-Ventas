namespace PosCaja.Application;

public sealed class VentaService(IVentaRepository repository) : IVentaService
{
    public Task<VentaDto?> ObtenerPorIdAsync(Guid idVenta, int idPos, CancellationToken cancellationToken = default)
        => repository.ObtenerPorIdAsync(idVenta, idPos, cancellationToken);

    public Task<PaginaResultado<VentaDto>> ObtenerPaginaAsync(int pagina = 1, int tamanoPagina = 50, CancellationToken cancellationToken = default)
    {
        if (pagina < 1)
            throw new PaginacionInvalidaException("La página debe ser mayor o igual a 1.");
        if (tamanoPagina is < 1 or > 100)
            throw new PaginacionInvalidaException("El tamaño de página debe estar entre 1 y 100.");
        if (((long)pagina - 1) * tamanoPagina > int.MaxValue)
            throw new PaginacionInvalidaException("La página solicitada supera el desplazamiento permitido.");
        return repository.ObtenerPaginaAsync(pagina, tamanoPagina, cancellationToken);
    }
}
