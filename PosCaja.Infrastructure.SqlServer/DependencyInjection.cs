using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PosCaja.Application;

namespace PosCaja.Infrastructure.SqlServer;

public static class DependencyInjection
{
    public static IServiceCollection AddPosCajaSqlServer(this IServiceCollection services, Action<SqlServerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        services.AddOptions<SqlServerOptions>().Configure(configure).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SqlServerOptions>, SqlServerOptionsValidator>());
        services.AddDbContext<PosCajaDbContext>((provider, options) =>
            options.UseSqlServer(provider.GetRequiredService<IOptions<SqlServerOptions>>().Value.BuildConnectionString()));
        services.AddScoped<IVentaRepository, VentaRepository>();
        return services;
    }
}
