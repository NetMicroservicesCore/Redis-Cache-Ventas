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

        public RedisJsonCache(
            IDistributedCache cache,
            ILogger<RedisJsonCache> logger)
        {
            this.cache = cache;
            this.logger = logger;
        }
    }
}
