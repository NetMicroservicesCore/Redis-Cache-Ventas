namespace PosCaja.Application;

public sealed class PaginacionInvalidaException(string mensaje) : Exception(mensaje);
