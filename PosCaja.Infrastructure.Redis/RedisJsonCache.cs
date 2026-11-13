using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace PosCaja.Infrastructure.Redis
{
    public sealed class RedisJsonCache
    {
        private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

        private readonly IDistributedCache cache;
        private readonly ILogger<RedisJsonCache> logger;

        public RedisJsonCache(IDistributedCache cache,ILogger<RedisJsonCache> logger)
        {
            this.cache = cache;
            this.logger = logger;
        }

        public async Task<CacheReadResult<T>> TryGetAsync<T>(string key,CancellationToken cancellationToken = default)where T : class
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            cancellationToken.ThrowIfCancellationRequested();
            byte[]? bytes;
            try
            {
                bytes = await cache.GetAsync(key, cancellationToken);
            }
            catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
            {
                cancellationToken.ThrowIfCancellationRequested();

                logger.LogWarning(
                    "Falló la lectura de caché para {DataType}. Tipo: {ErrorType}.",
                    typeof(T).Name,
                    exception.GetType().Name);

                return new CacheReadResult<T>(false, null);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (bytes is null)
            {
                logger.LogDebug(
                    "Entrada de caché ausente para {DataType}.",
                    typeof(T).Name);

                return new CacheReadResult<T>(false, null);
            }
            T? value;
            try
            {
                value = JsonSerializer.Deserialize<T>(
                    bytes,
                    JsonOptions);
            }
            catch (JsonException)
            {
                logger.LogWarning(
                    "JSON inválido en caché para {DataType}.",
                    typeof(T).Name);

                return new CacheReadResult<T>(false, null);
            }
            if (value is null)
            {
                logger.LogWarning(
                    "La entrada de caché contiene null para {DataType}.",
                    typeof(T).Name);

                return new CacheReadResult<T>(false, null);
            }
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogDebug(
                "Lectura de caché satisfactoria para {DataType}.",
                typeof(T).Name);

            return new CacheReadResult<T>(true, value);
        }


    }
}
