using System;
using System.Collections.Generic;
using System.Text;

namespace PosCaja.Infrastructure.Redis
{
    public sealed record CacheReadResult<T>(bool Found,T? Value) where T : class;
}
