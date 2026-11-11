using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
    }
}
