using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace PosCaja.Infrastructure.Redis
{
    internal class RedisCacheSettingsValidator : IValidateOptions<RedisCacheSettings>
    {
        public ValidateOptionsResult Validate(string? name, RedisCacheSettings options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (!options.Enabled)
            {
                return ValidateOptionsResult.Success;
            }
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(options.Host))
            {
                errors.Add("Redis:Host es obligatorio cuando Redis está activado.");
            }

            if (options.Port is < 1 or > 65535)
            {
                errors.Add("Redis:Port debe estar entre 1 y 65535.");
            }

            if (options.Database != 0)
            {
                errors.Add("Redis:Database debe ser 0 en esta versión.");
            }

            if (string.IsNullOrWhiteSpace(options.KeyPrefix))
            {
                errors.Add("Redis:KeyPrefix es obligatorio.");
            }
            if (options.TtlSeconds is < 1 or > 3600)
            {
                errors.Add("Redis:TtlSeconds debe estar entre 1 y 3600.");
            }

            if (options.ConnectTimeoutMilliseconds is < 100 or > 30000)
            {
                errors.Add(
                    "Redis:ConnectTimeoutMilliseconds debe estar entre 100 y 30000.");
            }

            if (options.AsyncTimeoutMilliseconds is < 100 or > 30000)
            {
                errors.Add(
                    "Redis:AsyncTimeoutMilliseconds debe estar entre 100 y 30000.");
            }

            if (!string.IsNullOrEmpty(options.User)
                && string.IsNullOrWhiteSpace(options.User))
            {
                errors.Add("Redis:User no puede contener únicamente espacios.");
            }
        }
    }
}
