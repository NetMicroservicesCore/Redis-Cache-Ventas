using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using PosCaja.Application;

namespace PosCaja.Infrastructure.Redis
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddPosCajaRedis(this IServiceCollection services, Action<RedisCacheSettings> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            services
            .AddOptions<RedisCacheSettings>()
            .Configure(configure)
            .ValidateOnStart();

            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<RedisCacheSettings>,
                    RedisCacheSettingsValidator>());

            services.AddStackExchangeRedisCache(_ => { });

            services
                .AddOptions<RedisCacheOptions>()
                .Configure<IOptions<RedisCacheSettings>>(
                    (redisOptions, settingsAccessor) =>
                    {
                        var settings = settingsAccessor.Value;

                        redisOptions.ConfigurationOptions =
                            settings.BuildConfigurationOptions();
                    });

            return services;
        }

        /// <summary>
        /// este metodo se encarga de recibir el contenedor actual de servicio y regresa el repositorio que se esta utilizando
        /// </summary>
        /// <param name="services">inyeccion del servicio</param>
        /// <param name="innerFactory">el contenedor del repositorio que s eutiliza.</param>
        /// <returns></returns>
        public static IServiceCollection AddCachedVentaRepository(this IServiceCollection services,Func<IServiceProvider,
            IVentaRepository> innerFactory)
        {
            ///evitamos que el innerFactory sea nulo, ya que es necesario para crear el repositorio interno.
            ArgumentNullException.ThrowIfNull(innerFactory);
            //registramos el repositorio de cache, para que pueda ser utilizado en el repositorio de ventas.
            services.TryAddSingleton<RedisJsonCache>();
            services.Replace(ServiceDescriptor.Scoped<IVentaRepository>(provider =>{
                    var options = provider
                        .GetRequiredService<IOptions<RedisCacheSettings>>();
                    var settings = options.Value;
                    var inner = innerFactory(provider);
                //obtenemos la configuracion de cache, si no esta habilitada, regresamos el repositorio interno.
                if (!settings.Enabled)
                    {
                        return inner;
                    }
                //obtenemos el cache y el logger, para poder crear el repositorio de cache.
                var cache = provider.GetRequiredService<RedisJsonCache>();
                    var logger = provider.GetRequiredService<ILogger<CachedVentaRepository>>();
                    return new CachedVentaRepository(inner,cache,options,logger);
                }));

            return services;
        }


    }
}
