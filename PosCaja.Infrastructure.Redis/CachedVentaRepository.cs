using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PosCaja.Application;
using System;
using System.Collections.Generic;
using System.Text;

namespace PosCaja.Infrastructure.Redis
{
    public sealed class CachedVentaRepository : IVentaRepository
    {

        private readonly IVentaRepository inner;
        private readonly RedisJsonCache cache;
        private readonly ILogger<CachedVentaRepository> logger;
        private readonly string keyPrefix;
        private readonly TimeSpan timeToLive;


        public CachedVentaRepository(IVentaRepository inner, RedisJsonCache cache, IOptions<RedisCacheSettings> options, ILogger<CachedVentaRepository> logger)
        {
            this.inner = inner;
            this.cache = cache;
            this.logger = logger;
            var settings = options.Value;
            keyPrefix = settings.KeyPrefix;
            timeToLive = TimeSpan.FromSeconds(settings.TtlSeconds);
        }

        public async Task<VentaDto?> ObtenerPorIdAsync(Guid idVenta, int idPos, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = VentaCacheKeys.ForVenta(keyPrefix,idVenta,idPos);
            var cached = await cache.TryGetAsync<VentaDto>(key,cancellationToken);
            if (cached.Found && cached.Value is { } cachedVenta)
            {
                if (cachedVenta.IdVenta == idVenta
                    && cachedVenta.IdPos == idPos)
                {
                    return cachedVenta;
                }

                logger.LogWarning(
                    "La venta en caché no coincide con la clave solicitada.");
            }
            var venta = await inner.ObtenerPorIdAsync(idVenta,idPos,cancellationToken);

            if (venta is null)
            {
                return null;
            }
            await cache.TrySetAsync(key,venta,timeToLive,cancellationToken);
            return venta;
        }

        public async Task<PaginaResultado<VentaDto>> ObtenerPaginaAsync(int pagina = 1,int tamanoPagina = 50,CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = VentaCacheKeys.ForPage(keyPrefix,pagina,tamanoPagina);

            var cached =await cache.TryGetAsync<PaginaResultado<VentaDto>>(key,cancellationToken);
            if (cached.Found && cached.Value is { } cachedPage)
            {
                if (cachedPage.Pagina == pagina
                    && cachedPage.TamanoPagina == tamanoPagina
                    && cachedPage.Items is not null
                    && cachedPage.Items.Count <= tamanoPagina
                    && cachedPage.TotalRegistros >= 0)
                {
                    return cachedPage;
                }
                logger.LogWarning("La página en caché no corresponde a una respuesta válida.");
            }
            var result = await inner.ObtenerPaginaAsync(pagina,tamanoPagina, cancellationToken);
            await cache.TrySetAsync(key,result,timeToLive,cancellationToken);
            return result;
        }

       
    }
}
