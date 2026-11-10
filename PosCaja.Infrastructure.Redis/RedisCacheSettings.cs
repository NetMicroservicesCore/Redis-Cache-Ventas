using Microsoft.Extensions.Options;
using StackExchange.Redis;
namespace PosCaja.Infrastructure.Redis
{
    public sealed class RedisCacheSettings
    {
        public const string SectionName = "Redis";

        public bool Enabled { get; set; } = false;

        public string Host { get; set; } = string.Empty;

        public int Port { get; set; } = 6379;

        public int Database { get; set; } = 0;

        public string? User { get; set; }

        public string? Password { get; set; }

        public bool Ssl { get; set; } = true;

        public string? SslHost { get; set; }

        public string KeyPrefix { get; set; } = string.Empty;

        public int TtlSeconds { get; set; } = 60;

        public int ConnectTimeoutMilliseconds { get; set; } = 1000;

        public int AsyncTimeoutMilliseconds { get; set; } = 1000;

        public ConfigurationOptions BuildConfigurationOptions()
        {
            if (!Enabled)
            {
                throw new InvalidOperationException(
                    "Redis está desactivado; no se debe construir su conexión.");
            }
            var validator = new RedisCacheSettingsValidator();
            var validation = validator.Validate(Options.DefaultName,this);

            if (validation.Failed)
            {
                throw new OptionsValidationException(
                    Options.DefaultName,
                    typeof(RedisCacheSettings),
                    validation.Failures);
            }

            var configuration = new ConfigurationOptions
            {
                DefaultDatabase = Database,
                Ssl = Ssl,
                ConnectTimeout = ConnectTimeoutMilliseconds,
                AsyncTimeout = AsyncTimeoutMilliseconds,
                ConnectRetry = 0,
                AbortOnConnectFail = false,
                BacklogPolicy = BacklogPolicy.FailFast
            };
            configuration.EndPoints.Add(Host, Port);


        }

    }
}
