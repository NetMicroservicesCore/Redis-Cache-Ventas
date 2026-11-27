using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PosCaja.Application;
using PosCaja.Infrastructure.Redis;
using PosCaja.Infrastructure.SqlServer;

namespace PosCaja.Tests;

public sealed class RedisDependencyInjectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolvesRepositoryAndServiceWithoutOpeningConnections(bool enabled)
    {
        var services = CreateServices(enabled);
        var connectionAttempts = 0;
        services.Configure<RedisCacheOptions>(options =>
            options.ConnectionMultiplexerFactory = () =>
            {
                connectionAttempts++;
                throw new InvalidOperationException("No se permiten conexiones en esta prueba.");
            });
        using var provider = BuildProvider(services);
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IVentaRepository>();
        var sql = scope.ServiceProvider.GetRequiredKeyedService<IVentaRepository>("sql");

        Assert.IsType<VentaRepository>(sql);
        if (enabled)
            Assert.IsType<CachedVentaRepository>(repository);
        else
            Assert.Same(sql, repository);
        Assert.IsType<VentaService>(scope.ServiceProvider.GetRequiredService<IVentaService>());
        Assert.Single(scope.ServiceProvider.GetServices<IVentaRepository>());
        Assert.Equal(ConnectionState.Closed, scope.ServiceProvider
            .GetRequiredService<PosCajaDbContext>().Database.GetDbConnection().State);
        Assert.Equal(0, connectionAttempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepositoriesAreReusedWithinScopeAndIsolatedBetweenScopes(bool enabled)
    {
        using var provider = BuildProvider(CreateServices(enabled));
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var repository = first.ServiceProvider.GetRequiredService<IVentaRepository>();

        Assert.Same(repository, first.ServiceProvider.GetRequiredService<IVentaRepository>());
        Assert.NotSame(repository, second.ServiceProvider.GetRequiredService<IVentaRepository>());
        Assert.NotSame(first.ServiceProvider.GetRequiredKeyedService<IVentaRepository>("sql"),
            second.ServiceProvider.GetRequiredKeyedService<IVentaRepository>("sql"));
    }

    [Fact]
    public async Task DisabledCacheQueriesSqlWithoutResolvingDistributedCache()
    {
        var services = CreateServices(false);
        var sql = new TestVentaRepository();
        services.AddKeyedScoped<IVentaRepository>("sql", (_, _) => sql);
        services.Replace(ServiceDescriptor.Singleton<IDistributedCache>(_ =>
            throw new InvalidOperationException("No debe resolverse Redis cuando está deshabilitado.")));
        using var provider = BuildProvider(services);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IVentaService>();

        var result = await service.ObtenerPorIdAsync(TestVentaRepository.VentaId, 2);

        Assert.NotNull(result);
        Assert.Equal(2, result.IdPos);
        Assert.Equal(1, sql.Calls);
    }

    [Fact]
    public async Task EnabledCacheSharesCachedSaleAcrossRequestScopes()
    {
        var services = CreateServices(true);
        var sql = new TestVentaRepository();
        services.AddKeyedScoped<IVentaRepository>("sql", (_, _) => sql);
        // Sustituir solo el transporte: conservar la composición real del decorador.
        services.RemoveAll<IDistributedCache>();
        services.AddDistributedMemoryCache();
        using var provider = BuildProvider(services);
        VentaDto? firstResult;
        using (var first = provider.CreateScope())
        {
            firstResult = await first.ServiceProvider.GetRequiredService<IVentaService>()
                .ObtenerPorIdAsync(TestVentaRepository.VentaId, 2);
        }
        using var second = provider.CreateScope();
        var secondResult = await second.ServiceProvider.GetRequiredService<IVentaService>()
            .ObtenerPorIdAsync(TestVentaRepository.VentaId, 2);

        Assert.NotNull(firstResult);
        Assert.Equal(firstResult, secondResult);
        Assert.Equal(1, sql.Calls);
    }

    private static ServiceCollection CreateServices(bool enabled)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IVentaService, VentaService>();
        services.AddPosCajaSqlServer(options =>
        {
            options.Server = "sql.example.test";
            options.Database = "pos_caja";
            options.IntegratedSecurity = true;
        });
        services.AddPosCajaRedis(settings =>
        {
            settings.Enabled = enabled;
            if (enabled)
            {
                settings.Host = "redis.example.test";
                settings.KeyPrefix = "tests:composition:";
            }
        });
        services.AddCachedVentaRepository(provider =>
            provider.GetRequiredKeyedService<IVentaRepository>("sql"));
        return services;
    }

    private static ServiceProvider BuildProvider(IServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
}
