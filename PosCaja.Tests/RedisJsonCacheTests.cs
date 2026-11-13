using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using PosCaja.Application;
using PosCaja.Infrastructure.Redis;
using StackExchange.Redis;

namespace PosCaja.Tests;

public sealed class RedisJsonCacheTests
{
    private const string Key = "tests:venta:private-key";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ValidSalePreservesValuesAndForwardsKeyAndCancellation()
    {
        var expected = new VentaDto
        {
            IdVenta = Guid.Parse("abcdef01-2345-6789-abcd-ef0123456789"),
            IdPos = 12,
            Vendedor = "private-seller",
            Folio = 9007199254740993L,
            FechaVenta = new DateTime(2026, 11, 13, 10, 30, 0, DateTimeKind.Unspecified),
            TotalVendido = 1234567890123456.789m,
            PagoEfectivo = 123.45m,
            PagoCheque = 23.456m,
            PagoVales = 10.01m,
            PagoTc = 20.02m,
            Supervisor = null,
            Upload = true,
            NumRegistros = 3
        };
        var backend = new ReadOnlyCacheDouble { Bytes = JsonSerializer.SerializeToUtf8Bytes(expected, JsonOptions) };
        var logger = new RecordingLogger();
        using var cancellation = new CancellationTokenSource();
        var sut = new RedisJsonCache(backend, logger);

        var result = await sut.TryGetAsync<VentaDto>(Key, cancellation.Token);

        Assert.True(result.Found);
        Assert.Equal(expected, result.Value);
        Assert.Equal(DateTimeKind.Unspecified, result.Value!.FechaVenta.Kind);
        Assert.Equal(Key, backend.LastKey);
        Assert.Equal(cancellation.Token, backend.LastToken);
        Assert.Equal(1, backend.Reads);
        Assert.DoesNotContain("private-seller", logger.Text);
        Assert.DoesNotContain(Key, logger.Text);
    }

    [Fact]
    public async Task PagePreservesItemsMetadataAndLongTotal()
    {
        var expected = new PaginaResultado<VentaDto>(
            [new VentaDto { IdPos = 2, Vendedor = "test" }], 3, 50, 3000000000L);
        var backend = new ReadOnlyCacheDouble { Bytes = JsonSerializer.SerializeToUtf8Bytes(expected, JsonOptions) };

        var result = await new RedisJsonCache(backend, new RecordingLogger())
            .TryGetAsync<PaginaResultado<VentaDto>>(Key);

        Assert.True(result.Found);
        Assert.NotNull(result.Value);
        Assert.Equal(expected.Pagina, result.Value.Pagina);
        Assert.Equal(expected.TamanoPagina, result.Value.TamanoPagina);
        Assert.Equal(expected.TotalRegistros, result.Value.TotalRegistros);
        Assert.Equal(expected.Items.Single(), Assert.Single(result.Value.Items));
    }

    [Fact]
    public async Task MissingEntryReturnsMiss()
    {
        var backend = new ReadOnlyCacheDouble();

        var result = await new RedisJsonCache(backend, new RecordingLogger()).TryGetAsync<VentaDto>(Key);

        Assert.False(result.Found);
        Assert.Null(result.Value);
        Assert.Equal(1, backend.Reads);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{broken-private-payload")]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task UnusableJsonReturnsMissWithoutLoggingPayload(string payload)
    {
        var backend = new ReadOnlyCacheDouble { Bytes = Encoding.UTF8.GetBytes(payload) };
        var logger = new RecordingLogger();

        var result = await new RedisJsonCache(backend, logger).TryGetAsync<VentaDto>(Key);

        Assert.False(result.Found);
        Assert.Null(result.Value);
        Assert.Contains(LogLevel.Warning, logger.Levels);
        Assert.DoesNotContain("broken-private-payload", logger.Text);
        Assert.DoesNotContain(Key, logger.Text);
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("timeout")]
    [InlineData("server")]
    public async Task RedisFailuresReturnMissWithoutLoggingSensitiveException(string failure)
    {
        const string sensitiveMessage = "private-host private-password";
        Exception exception = failure switch
        {
            "connection" => new RedisConnectionException(ConnectionFailureType.UnableToConnect, sensitiveMessage),
            "timeout" => new RedisTimeoutException(sensitiveMessage, CommandStatus.Unknown),
            _ => new RedisServerException(sensitiveMessage)
        };
        var backend = new ReadOnlyCacheDouble { OnRead = _ => throw exception };
        var logger = new RecordingLogger();

        var result = await new RedisJsonCache(backend, logger).TryGetAsync<VentaDto>(Key);

        Assert.False(result.Found);
        Assert.Null(result.Value);
        Assert.Contains(LogLevel.Warning, logger.Levels);
        Assert.DoesNotContain(sensitiveMessage, logger.Text);
        Assert.DoesNotContain(Key, logger.Text);
        Assert.All(logger.Exceptions, captured => Assert.Null(captured));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidKeyIsRejectedBeforeReading(string? key)
    {
        var backend = new ReadOnlyCacheDouble();
        var sut = new RedisJsonCache(backend, new RecordingLogger());

        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.TryGetAsync<VentaDto>(key!));

        Assert.Equal(0, backend.Reads);
    }

    [Fact]
    public async Task AlreadyCancelledRequestDoesNotReadCache()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var backend = new ReadOnlyCacheDouble();
        var sut = new RedisJsonCache(backend, new RecordingLogger());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.TryGetAsync<VentaDto>(Key, cancellation.Token));

        Assert.Equal(0, backend.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringReadIsNotConvertedToMiss(bool redisFailure)
    {
        using var cancellation = new CancellationTokenSource();
        var backend = new ReadOnlyCacheDouble
        {
            OnRead = _ =>
            {
                cancellation.Cancel();
                if (redisFailure)
                    throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "test");
                return Task.FromResult<byte[]?>(null);
            }
        };
        var sut = new RedisJsonCache(backend, new RecordingLogger());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.TryGetAsync<VentaDto>(Key, cancellation.Token));
    }

    [Fact]
    public async Task ProviderCancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        var backend = new ReadOnlyCacheDouble
        {
            OnRead = token =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<byte[]?>(token);
            }
        };
        var sut = new RedisJsonCache(backend, new RecordingLogger());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.TryGetAsync<VentaDto>(Key, cancellation.Token));
    }

    [Fact]
    public async Task UnexpectedProgrammingErrorPropagates()
    {
        var expected = new InvalidOperationException("test");
        var backend = new ReadOnlyCacheDouble { OnRead = _ => throw expected };
        var sut = new RedisJsonCache(backend, new RecordingLogger());

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.TryGetAsync<VentaDto>(Key));

        Assert.Same(expected, actual);
    }

    private sealed class ReadOnlyCacheDouble : IDistributedCache
    {
        public byte[]? Bytes { get; init; }
        public Func<CancellationToken, Task<byte[]?>>? OnRead { get; init; }
        public int Reads { get; private set; }
        public string? LastKey { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            Reads++;
            LastKey = key;
            LastToken = token;
            return OnRead is null ? Task.FromResult(Bytes) : OnRead(token);
        }

        // Una lectura no debe usar operaciones síncronas, escribir, borrar ni renovar TTL.
        public byte[]? Get(string key) => throw new NotSupportedException();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotSupportedException();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw new NotSupportedException();
        public void Refresh(string key) => throw new NotSupportedException();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
        public void Remove(string key) => throw new NotSupportedException();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
    }

    private sealed class RecordingLogger : ILogger<RedisJsonCache>
    {
        private readonly List<string> messages = [];
        public List<LogLevel> Levels { get; } = [];
        public List<Exception?> Exceptions { get; } = [];
        public string Text => string.Join("\n", messages);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Levels.Add(logLevel);
            Exceptions.Add(exception);
            messages.Add(formatter(state, exception));
        }
    }
}
