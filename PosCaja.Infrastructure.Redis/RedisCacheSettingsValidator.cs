using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace PosCaja.Infrastructure.Redis
{
    public sealed class RedisCacheSettingsValidator
    : IValidateOptions<RedisCacheSettings>
    {
        public ValidateOptionsResult Validate(
            string? name,
            RedisCacheSettings options)
        {
            if (!options.Enabled)
            {
                return ValidateOptionsResult.Success;
            }

            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(options.Host))
            {
                errors.Add("Redis:Host es obligatorio.");
            }

            if (options.Port is < 1 or > 65535)
            {
                errors.Add("Redis:Port debe estar entre 1 y 65535.");
            }

            if (options.Database != 0)
            {
                errors.Add("Esta versión admite únicamente Redis:Database=0.");
            }

            if (string.IsNullOrWhiteSpace(options.KeyPrefix))
            {
                errors.Add("Redis:KeyPrefix es obligatorio.");
            }
            else if (!options.KeyPrefix.EndsWith(':'))
            {
                errors.Add("Redis:KeyPrefix debe terminar en ':'.");
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
                && string.IsNullOrEmpty(options.Password))
            {
                errors.Add(
                    "Redis:Password es obligatorio cuando se configura Redis:User.");
            }

            return errors.Count == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(errors);
        }
    }
}
