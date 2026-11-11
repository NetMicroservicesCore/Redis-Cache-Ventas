using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PosCaja.Infrastructure.Redis;

namespace PosCaja.Tests;

public sealed class RedisStartupTests
{
    [Fact]
    public async Task DisabledCacheStartsWithoutConnectionSettings()
    {
        using var host = new HostBuilder()
            .ConfigureServices(services =>
                services.AddPosCajaRedis(settings => settings.Enabled = false))
            .Build();

        await host.StartAsync();

        Assert.False(host.Services.GetRequiredService<IOptions<RedisCacheSettings>>().Value.Enabled);
        await host.StopAsync();
    }

    [Theory]
    [InlineData("Host", "")]
    [InlineData("KeyPrefix", "")]
    [InlineData("Port", "0")]
    public async Task EnabledCacheRejectsInvalidSettingsAtStartup(string key, string value)
    {
        var values = ValidConfiguration();
        values["Redis:" + key] = value;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddPosCajaRedis(settings =>
                configuration.GetSection(RedisCacheSettings.SectionName).Bind(settings)))
            .Build();

        // No resolver IDistributedCache: ValidateOnStart debe detectar el error por sí solo.
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains(exception.Failures, failure => failure.Contains("Redis:" + key));
        Assert.DoesNotContain("test-password", exception.Message);
    }

    [Fact]
    public async Task RegistrationBindsSettingsToProviderWithoutOpeningConnection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(ValidConfiguration()).Build();
        var connectionAttempts = 0;
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddPosCajaRedis(settings =>
                    configuration.GetSection(RedisCacheSettings.SectionName).Bind(settings));
                // Impedir conexiones reales y detectar cualquier apertura anticipada.
                services.Configure<RedisCacheOptions>(options =>
                    options.ConnectionMultiplexerFactory = () =>
                    {
                        Interlocked.Increment(ref connectionAttempts);
                        throw new InvalidOperationException("No se permiten conexiones en esta prueba.");
                    });
            })
            .Build();

        await host.StartAsync();
        var settings = host.Services.GetRequiredService<IOptions<RedisCacheSettings>>().Value;
        var providerOptions = host.Services.GetRequiredService<IOptions<RedisCacheOptions>>().Value;
        var cache = host.Services.GetRequiredService<IDistributedCache>();

        Assert.IsAssignableFrom<RedisCache>(cache);
        Assert.Same(cache, host.Services.GetRequiredService<IDistributedCache>());
        Assert.Equal("tests:startup:", settings.KeyPrefix);
        Assert.Equal(60, settings.TtlSeconds);
        var connection = Assert.IsType<StackExchange.Redis.ConfigurationOptions>(
            providerOptions.ConfigurationOptions);
        var endpoint = Assert.IsType<System.Net.DnsEndPoint>(Assert.Single(connection.EndPoints));
        Assert.Equal("redis.example.test", endpoint.Host);
        Assert.Equal(6380, endpoint.Port);
        Assert.True(connection.Ssl);
        Assert.Equal("test-user", connection.User);
        Assert.Equal("test-password", connection.Password);
        Assert.Equal(1700, connection.ConnectTimeout);
        Assert.Equal(2400, connection.AsyncTimeout);
        Assert.Equal(0, connectionAttempts);

        await host.StopAsync();
        Assert.Equal(0, connectionAttempts);
    }

    private static Dictionary<string, string?> ValidConfiguration() => new()
    {
        ["Redis:Enabled"] = "true",
        ["Redis:Host"] = "redis.example.test",
        ["Redis:Port"] = "6380",
        ["Redis:KeyPrefix"] = "tests:startup:",
        ["Redis:User"] = "test-user",
        ["Redis:Password"] = "test-password",
        ["Redis:Ssl"] = "true",
        ["Redis:ConnectTimeoutMilliseconds"] = "1700",
        ["Redis:AsyncTimeoutMilliseconds"] = "2400"
    };
}
