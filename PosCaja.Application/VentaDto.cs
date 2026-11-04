namespace PosCaja.Application;

public sealed record VentaDto
{
    public Guid IdVenta { get; init; }
    public int IdPos { get; init; }
    public required string Vendedor { get; init; }
    public long Folio { get; init; }
    public DateTime FechaVenta { get; init; }
    public decimal TotalVendido { get; init; }
    public decimal PagoEfectivo { get; init; }
    public decimal PagoCheque { get; init; }
    public decimal PagoVales { get; init; }
    public decimal PagoTc { get; init; }
    public string? Supervisor { get; init; }
    public bool Upload { get; init; }
    public short NumRegistros { get; init; }
}
