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
            throw new NotImplementedException();
        }
    }
}
