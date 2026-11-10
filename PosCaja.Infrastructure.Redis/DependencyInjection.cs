using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace PosCaja.Infrastructure.Redis
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddPosCajaRedis(this IServiceCollection services, Action<RedisCacheSettings> configure)
        {

        }
    }
}
