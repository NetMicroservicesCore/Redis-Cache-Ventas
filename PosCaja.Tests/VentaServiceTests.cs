using PosCaja.Application;

namespace PosCaja.Tests;

public sealed class VentaServiceTests
{
    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public async Task InvalidPaginationDoesNotReachRepository(int page, int size)
    {
        var repository = new TestVentaRepository();
        var service = new VentaService(repository);
        await Assert.ThrowsAsync<PaginacionInvalidaException>(() => service.ObtenerPaginaAsync(page, size));
        Assert.Equal(0, repository.Calls);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    [InlineData(3, 50)]
    public async Task ValidPaginationAndCancellationAreForwarded(int page, int size)
    {
        var repository = new TestVentaRepository();
        using var source = new CancellationTokenSource();
        var result = await new VentaService(repository).ObtenerPaginaAsync(page, size, source.Token);
        Assert.Equal(page, result.Pagina);
        Assert.Equal(size, result.TamanoPagina);
        Assert.Equal(source.Token, repository.LastToken);
    }

    [Fact]
    public async Task CompositeKeyAndCancellationAreForwarded()
    {
        var repository = new TestVentaRepository();
        using var source = new CancellationTokenSource();
        var service = new VentaService(repository);
        var first = await service.ObtenerPorIdAsync(TestVentaRepository.VentaId, 1, source.Token);
        var second = await service.ObtenerPorIdAsync(TestVentaRepository.VentaId, 2, source.Token);
        Assert.Equal(1, first!.IdPos);
        Assert.Equal(2, second!.IdPos);
        Assert.Null(await service.ObtenerPorIdAsync(Guid.NewGuid(), 1, source.Token));
        Assert.Equal(source.Token, repository.LastToken);
    }
}
