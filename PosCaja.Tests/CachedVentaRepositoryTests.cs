using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PosCaja.Application;
using PosCaja.Infrastructure.Redis;
using StackExchange.Redis;

namespace PosCaja.Tests;

public sealed class CachedVentaRepositoryTests
{
    private const string Prefix = "tests:ventas:";
    private static readonly Guid Id = Guid.Parse("abcdef01-2345-6789-abcd-ef0123456789");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static VentaDto Sale(int pos = 2) => new()
    {
        IdVenta = Id, IdPos = pos, Vendedor = "test", TotalVendido = 123.456m
    };
    private static CachedVentaRepository Create(SqlDouble sql, CacheDouble redis, int ttl = 60)
        => new(sql, new RedisJsonCache(redis, NullLogger<RedisJsonCache>.Instance),
            Options.Create(new RedisCacheSettings { Enabled = true, KeyPrefix = Prefix, TtlSeconds = ttl }),
            NullLogger<CachedVentaRepository>.Instance);

    [Fact]
    public async Task SaleHitAvoidsSqlAndDoesNotRewriteCache()
    {
        var sql = new SqlDouble { Failure = new InvalidOperationException("SQL unavailable") };
        var redis = new CacheDouble();
        redis.Seed(VentaCacheKeys.ForVenta(Prefix, Id, 2), Sale());

        var result = await Create(sql, redis).ObtenerPorIdAsync(Id, 2);

        Assert.Equal(Sale(), result);
        Assert.Equal(0, sql.Calls);
        Assert.Equal(0, redis.Writes);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(120)]
    public async Task SaleMissCachesSqlResultAndNextRequestHits(int ttl)
    {
        var sql = new SqlDouble { Venta = Sale() };
        var redis = new CacheDouble();
        var repository = Create(sql, redis, ttl);
        using var source = new CancellationTokenSource();

        var first = await repository.ObtenerPorIdAsync(Id, 2, source.Token);
        var second = await repository.ObtenerPorIdAsync(Id, 2, source.Token);

        Assert.Same(sql.Venta, first);
        Assert.Equal(first, second);
        Assert.Equal((Id, 2), sql.LastSale);
        Assert.Equal(source.Token, sql.LastToken);
        Assert.Equal(source.Token, redis.LastToken);
        Assert.Equal(1, sql.Calls);
        Assert.Equal(1, redis.Writes);
        Assert.Equal(TimeSpan.FromSeconds(ttl), redis.LastOptions!.AbsoluteExpirationRelativeToNow);
        Assert.Null(redis.LastOptions.SlidingExpiration);
        Assert.Equal(VentaCacheKeys.ForVenta(Prefix, Id, 2), redis.LastKey);
    }

    [Fact]
    public async Task MissingSaleIsNotCached()
    {
        var sql = new SqlDouble();
        var redis = new CacheDouble();
        var repository = Create(sql, redis);

        Assert.Null(await repository.ObtenerPorIdAsync(Id, 2));
        Assert.Null(await repository.ObtenerPorIdAsync(Id, 2));
        Assert.Equal(2, sql.Calls);
        Assert.Equal(0, redis.Writes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MismatchedSaleIdentityIsReplacedFromSql(bool wrongGuid)
    {
        var sql = new SqlDouble { Venta = Sale() };
        var redis = new CacheDouble();
        var key = VentaCacheKeys.ForVenta(Prefix, Id, 2);
        redis.Seed(key, wrongGuid ? Sale() with { IdVenta = Guid.Empty } : Sale(3));
        var repository = Create(sql, redis);

        Assert.Equal(Sale(), await repository.ObtenerPorIdAsync(Id, 2));
        Assert.Equal(Sale(), await repository.ObtenerPorIdAsync(Id, 2));
        Assert.Equal(1, sql.Calls);
        Assert.Equal(1, redis.Writes);
    }

    [Fact]
    public async Task DifferentPosDoesNotReuseExistingSale()
    {
        var sql = new SqlDouble { Venta = Sale(3) };
        var redis = new CacheDouble();
        redis.Seed(VentaCacheKeys.ForVenta(Prefix, Id, 2), Sale());

        Assert.Equal(Sale(3), await Create(sql, redis).ObtenerPorIdAsync(Id, 3));
        Assert.Equal((Id, 3), sql.LastSale);
        Assert.Equal(1, sql.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task PageMissCachesCompleteResultIncludingEmptyPage(int itemCount)
    {
        var page = new PaginaResultado<VentaDto>(itemCount == 0 ? [] : [Sale()], 2, 50, 3000000000L);
        var sql = new SqlDouble { Page = page };
        var redis = new CacheDouble();
        var repository = Create(sql, redis);
        using var source = new CancellationTokenSource();

        Assert.Same(page, await repository.ObtenerPaginaAsync(2, 50, source.Token));
        var hit = await repository.ObtenerPaginaAsync(2, 50, source.Token);

        Assert.Equal(itemCount, hit.Items.Count);
        Assert.Equal(page.TotalRegistros, hit.TotalRegistros);
        Assert.Equal(2, hit.Pagina);
        Assert.Equal(50, hit.TamanoPagina);
        Assert.Equal((2, 50), sql.LastPage);
        Assert.Equal(source.Token, sql.LastToken);
        Assert.Equal(source.Token, redis.LastToken);
        Assert.Equal(1, sql.Calls);
        Assert.Equal(1, redis.Writes);
    }

    [Fact]
    public async Task ValidPageHitAvoidsUnavailableSql()
    {
        var sql = new SqlDouble { Failure = new InvalidOperationException("SQL unavailable") };
        var redis = new CacheDouble();
        redis.Seed(VentaCacheKeys.ForPage(Prefix, 1, 50),
            new PaginaResultado<VentaDto>([Sale()], 1, 50, 1));

        var result = await Create(sql, redis).ObtenerPaginaAsync();

        Assert.Equal(Sale(), Assert.Single(result.Items));
        Assert.Equal(0, sql.Calls);
        Assert.Equal(0, redis.Writes);
    }

    [Theory]
    [InlineData("page")]
    [InlineData("size")]
    [InlineData("null-items")]
    [InlineData("too-many-items")]
    [InlineData("negative-total")]
    public async Task InvalidPageIsReplacedFromSql(string defect)
    {
        var invalid = new PaginaResultado<VentaDto>([], 1, 1, 0);
        invalid = defect switch
        {
            "page" => invalid with { Pagina = 2 },
            "size" => invalid with { TamanoPagina = 2 },
            "null-items" => invalid with { Items = null! },
            "too-many-items" => invalid with { Items = new[] { Sale(), Sale(3) } },
            _ => invalid with { TotalRegistros = -1 }
        };
        var sql = new SqlDouble { Page = new([Sale()], 1, 1, 1) };
        var redis = new CacheDouble();
        redis.Seed(VentaCacheKeys.ForPage(Prefix, 1, 1), invalid);
        var repository = Create(sql, redis);

        Assert.Same(sql.Page, await repository.ObtenerPaginaAsync(1, 1));
        Assert.Equal(Sale(), Assert.Single((await repository.ObtenerPaginaAsync(1, 1)).Items));
        Assert.Equal(1, sql.Calls);
        Assert.Equal(1, redis.Writes);
    }

    [Theory]
    [InlineData(2, 50)]
    [InlineData(1, 10)]
    public async Task DifferentPageParametersDoNotReuseCachedPage(int page, int size)
    {
        var sql = new SqlDouble { Page = new([], page, size, 0) };
        var redis = new CacheDouble();
        redis.Seed(VentaCacheKeys.ForPage(Prefix, 1, 50), new PaginaResultado<VentaDto>([], 1, 50, 0));

        Assert.Same(sql.Page, await Create(sql, redis).ObtenerPaginaAsync(page, size));
        Assert.Equal((page, size), sql.LastPage);
        Assert.Equal(1, sql.Calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RedisFailureStillReturnsSqlResult(bool page, bool readFailure)
    {
        var sql = new SqlDouble { Venta = Sale(), Page = new([], 1, 50, 0) };
        var redis = new CacheDouble
        {
            ReadFailure = readFailure ? new RedisTimeoutException("test", CommandStatus.Unknown) : null,
            WriteFailure = new RedisServerException("test")
        };
        var repository = Create(sql, redis);

        if (page) Assert.Same(sql.Page, await repository.ObtenerPaginaAsync());
        else Assert.Same(sql.Venta, await repository.ObtenerPorIdAsync(Id, 2));
        Assert.Equal(1, sql.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CorruptJsonFallsBackToSql(bool page)
    {
        var sql = new SqlDouble { Venta = Sale(), Page = new([], 1, 50, 0) };
        var redis = new CacheDouble();
        redis.Data[page ? VentaCacheKeys.ForPage(Prefix, 1, 50) : VentaCacheKeys.ForVenta(Prefix, Id, 2)] =
            Encoding.UTF8.GetBytes("{broken");
        var repository = Create(sql, redis);

        if (page) Assert.Same(sql.Page, await repository.ObtenerPaginaAsync());
        else Assert.Same(sql.Venta, await repository.ObtenerPorIdAsync(Id, 2));
        Assert.Equal(1, sql.Calls);
        Assert.Equal(1, redis.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SqlFailurePropagatesWithoutWritingCache(bool page)
    {
        var failure = new InvalidOperationException("SQL failure");
        var sql = new SqlDouble { Failure = failure };
        var redis = new CacheDouble();
        var repository = Create(sql, redis);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            if (page) await repository.ObtenerPaginaAsync();
            else await repository.ObtenerPorIdAsync(Id, 2);
        });
        Assert.Same(failure, actual);
        Assert.Equal(0, redis.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AlreadyCancelledRequestDoesNoIo(bool page)
    {
        var sql = new SqlDouble();
        var redis = new CacheDouble();
        var repository = Create(sql, redis);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            if (page) await repository.ObtenerPaginaAsync(cancellationToken: source.Token);
            else await repository.ObtenerPorIdAsync(Id, 2, source.Token);
        });
        Assert.Equal(0, sql.Calls);
        Assert.Equal(0, redis.Reads);
        Assert.Equal(0, redis.Writes);
    }

    private sealed class SqlDouble : IVentaRepository
    {
        public VentaDto? Venta { get; init; }
        public PaginaResultado<VentaDto> Page { get; init; } = new([], 1, 50, 0);
        public Exception? Failure { get; init; }
        public int Calls { get; private set; }
        public (Guid, int) LastSale { get; private set; }
        public (int, int) LastPage { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public Task<VentaDto?> ObtenerPorIdAsync(Guid idVenta, int idPos, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastSale = (idVenta, idPos);
            LastToken = cancellationToken;
            if (Failure is not null) throw Failure;
            return Task.FromResult(Venta);
        }
        public Task<PaginaResultado<VentaDto>> ObtenerPaginaAsync(int pagina = 1, int tamanoPagina = 50, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastPage = (pagina, tamanoPagina);
            LastToken = cancellationToken;
            if (Failure is not null) throw Failure;
            return Task.FromResult(Page);
        }
    }

    private sealed class CacheDouble : IDistributedCache
    {
        public Dictionary<string, byte[]> Data { get; } = [];
        public Exception? ReadFailure { get; init; }
        public Exception? WriteFailure { get; init; }
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public string? LastKey { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public DistributedCacheEntryOptions? LastOptions { get; private set; }
        public void Seed<T>(string key, T value) => Data[key] = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            Reads++;
            LastKey = key;
            LastToken = token;
            if (ReadFailure is not null) throw ReadFailure;
            return Task.FromResult(Data.TryGetValue(key, out var value) ? value : null);
        }
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Writes++;
            LastKey = key;
            LastToken = token;
            LastOptions = options;
            if (WriteFailure is not null) throw WriteFailure;
            Data[key] = value;
            return Task.CompletedTask;
        }
        public byte[]? Get(string key) => throw new NotSupportedException();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotSupportedException();
        public void Refresh(string key) => throw new NotSupportedException();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
        public void Remove(string key) => throw new NotSupportedException();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
    }
}
