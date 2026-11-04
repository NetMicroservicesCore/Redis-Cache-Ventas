namespace PosCaja.Application;

public sealed record PaginaResultado<T>(IReadOnlyList<T> Items, int Pagina, int TamanoPagina, long TotalRegistros);
