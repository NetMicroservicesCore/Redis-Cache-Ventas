using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using PosCaja.Application;
using PosCaja.Infrastructure.Redis;
using StackExchange.Redis;

namespace PosCaja.Tests;

public sealed class RedisJsonCacheWriteTests
{
    private const string Key = "tests:venta:1";
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private static RedisJsonCache CreateCache(WriteCacheDouble backend)
        => new(backend, NullLogger<RedisJsonCache>.Instance);
    private static VentaDto Sale() => new()
    {
        IdVenta = Guid.Parse("abcdef01-2345-6789-abcd-ef0123456789"),
        IdPos = 12, Vendedor = "test", Folio = 9007199254740993L,
        FechaVenta = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Unspecified),
        TotalVendido = 1234567890123456.789m, PagoCheque = 12.345m,
        PagoEfectivo = 10.12m, Supervisor = null
    };

    [Fact]
    public async Task SaleIsStoredAsWebJsonWithAbsoluteExpirationAndCancellation()
    {
        var backend = new WriteCacheDouble();
        var sale = Sale();
        using var source = new CancellationTokenSource();

        await CreateCache(backend).TrySetAsync(Key, sale, Ttl, source.Token);

        Assert.Equal(1, backend.Writes);
        Assert.Equal(Key, backend.Key);
        Assert.Equal(source.Token, backend.Token);
        Assert.NotNull(backend.Options);
        Assert.Equal(Ttl, backend.Options.AbsoluteExpirationRelativeToNow);
        Assert.Null(backend.Options.SlidingExpiration);
        Assert.Null(backend.Options.AbsoluteExpiration);
        using var json = JsonDocument.Parse(backend.Bytes!);
        Assert.Equal(sale.TotalVendido, json.RootElement.GetProperty("totalVendido").GetDecimal());
        Assert.Equal(sale.Folio, json.RootElement.GetProperty("folio").GetInt64());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("supervisor").ValueKind);
        var restored = JsonSerializer.Deserialize<VentaDto>(
            backend.Bytes!, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(sale, restored);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task PagesIncludingEmptyPagesPreserveMetadata(int count)
    {
        var backend = new WriteCacheDouble();
        var page = new PaginaResultado<VentaDto>(count == 0 ? [] : [Sale()], 2, 50, 3000000000L);

        await CreateCache(backend).TrySetAsync(Key, page, Ttl);

        var restored = JsonSerializer.Deserialize<PaginaResultado<VentaDto>>(
            backend.Bytes!, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(restored);
        Assert.Equal(count, restored.Items.Count);
        Assert.Equal(page.TotalRegistros, restored.TotalRegistros);
        Assert.Equal(page.Pagina, restored.Pagina);
        Assert.Equal(page.TamanoPagina, restored.TamanoPagina);
        if (count > 0) Assert.Equal(page.Items[0], restored.Items[0]);
    }

    [Fact]
    public async Task CallerSuppliedExpirationIsNotHardcoded()
    {
        var backend = new WriteCacheDouble();

        await CreateCache(backend).TrySetAsync(Key, Sale(), TimeSpan.FromSeconds(120));

        Assert.Equal(TimeSpan.FromSeconds(120), backend.Options!.AbsoluteExpirationRelativeToNow);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task InvalidKeysDoNotWrite(string? key)
    {
        var backend = new WriteCacheDouble();
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => CreateCache(backend).TrySetAsync(key!, Sale(), Ttl));
        Assert.Equal(0, backend.Writes);
    }

    [Fact]
    public async Task NullValueDoesNotWrite()
    {
        var backend = new WriteCacheDouble();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => CreateCache(backend).TrySetAsync<VentaDto>(Key, null!, Ttl));
        Assert.Equal(0, backend.Writes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonPositiveExpirationDoesNotWrite(int seconds)
    {
        var backend = new WriteCacheDouble();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => CreateCache(backend).TrySetAsync(Key, Sale(), TimeSpan.FromSeconds(seconds)));
        Assert.Equal(0, backend.Writes);
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("timeout")]
    [InlineData("server")]
    public async Task RedisFailureDoesNotEscapeWrite(string kind)
    {
        Exception failure = kind switch
        {
            "connection" => new RedisConnectionException(ConnectionFailureType.UnableToConnect, "test"),
            "timeout" => new RedisTimeoutException("test", CommandStatus.Unknown),
            _ => new RedisServerException("test")
        };
        var backend = new WriteCacheDouble { OnWrite = _ => throw failure };

        await CreateCache(backend).TrySetAsync(Key, Sale(), Ttl);

        Assert.Equal(1, backend.Writes);
    }

    [Fact]
    public async Task PreCancelledRequestDoesNotWrite()
    {
        var backend = new WriteCacheDouble();
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateCache(backend).TrySetAsync(Key, Sale(), Ttl, source.Token));
        Assert.Equal(0, backend.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringWriteIsNotSwallowed(bool redisFailure)
    {
        using var source = new CancellationTokenSource();
        var backend = new WriteCacheDouble
        {
            OnWrite = _ =>
            {
                source.Cancel();
                if (redisFailure) throw new RedisServerException("test");
                return Task.CompletedTask;
            }
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateCache(backend).TrySetAsync(Key, Sale(), Ttl, source.Token));
    }

    [Fact]
    public async Task ProviderCancellationPropagates()
    {
        using var source = new CancellationTokenSource();
        var backend = new WriteCacheDouble
        {
            OnWrite = token =>
            {
                source.Cancel();
                return Task.FromCanceled(token);
            }
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateCache(backend).TrySetAsync(Key, Sale(), Ttl, source.Token));
    }

    [Fact]
    public async Task UnexpectedProviderErrorPropagates()
    {
        var failure = new InvalidOperationException("test");
        var backend = new WriteCacheDouble { OnWrite = _ => throw failure };
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateCache(backend).TrySetAsync(Key, Sale(), Ttl));
        Assert.Same(failure, actual);
    }

    [Fact]
    public async Task JsonCycleDoesNotReachProvider()
    {
        var backend = new WriteCacheDouble();
        var value = new CyclicValue();
        value.Next = value;

        await CreateCache(backend).TrySetAsync(Key, value, Ttl);

        Assert.Equal(0, backend.Writes);
    }

    [Fact]
    public async Task UnsupportedSerializationTypePropagates()
    {
        var backend = new WriteCacheDouble();
        await Assert.ThrowsAsync<NotSupportedException>(
            () => CreateCache(backend).TrySetAsync(Key, new { Callback = (Action)(() => { }) }, Ttl));
        Assert.Equal(0, backend.Writes);
    }

    private sealed class CyclicValue
    {
        public CyclicValue? Next { get; set; }
    }

    private sealed class WriteCacheDouble : IDistributedCache
    {
        public int Writes { get; private set; }
        public string? Key { get; private set; }
        public byte[]? Bytes { get; private set; }
        public DistributedCacheEntryOptions? Options { get; private set; }
        public CancellationToken Token { get; private set; }
        public Func<CancellationToken, Task>? OnWrite { get; init; }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default)
        {
            Writes++;
            Key = key;
            Bytes = value;
            Options = options;
            Token = token;
            return OnWrite?.Invoke(token) ?? Task.CompletedTask;
        }

        public byte[]? Get(string key) => throw new NotSupportedException();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotSupportedException();
        public void Refresh(string key) => throw new NotSupportedException();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
        public void Remove(string key) => throw new NotSupportedException();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
    }
}
