using Microsoft.Extensions.Options;
using PosCaja.Infrastructure.Redis;
using System;
using System.Collections.Generic;
using System.Text;

namespace PosCaja.Tests
{
    public sealed class RedisCacheSettingsValidatorTests
    {
        private readonly RedisCacheSettingsValidator validator = new();

        private static RedisCacheSettings CreateValidSettings()
        {
            return new RedisCacheSettings
            {
                Enabled = true,
                Host = "localhost",
                KeyPrefix = "Api.Redis:Development:pos_caja:venta:v1:"
            };
        }

        [Fact]
        public void DisabledCacheDoesNotRequireConnectionSettings()
        {
            var settings = new RedisCacheSettings
            {
                Enabled = false
            };

            var result = validator.Validate(Options.DefaultName, settings);

            Assert.True(result.Succeeded);
        }

        [Fact]
        public void ValidSettingsPassValidation()
        {
            var settings = CreateValidSettings();

            var result = validator.Validate(Options.DefaultName, settings);

            Assert.True(result.Succeeded);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void EmptyHostFailsValidation(string host)
        {
            var settings = CreateValidSettings();
            settings.Host = host;

            var result = validator.Validate(Options.DefaultName, settings);

            Assert.True(result.Failed);
            Assert.Contains("Redis:Host es obligatorio.", result.Failures);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(65535, true)]
        [InlineData(65536, false)]
        public void PortMustBeWithinAllowedRange(int port, bool expectedSuccess)
        {
            var settings = CreateValidSettings();
            settings.Port = port;

            var result = validator.Validate(Options.DefaultName, settings);

            Assert.Equal(expectedSuccess, result.Succeeded);
        }

        [Theory]
        [InlineData(-1, false)]
        [InlineData(0, true)]
        [InlineData(1, false)]
        public void OnlyDatabaseZeroIsSupported(
        int database,
        bool expectedSuccess)
        {
            var settings = CreateValidSettings();
            settings.Database = database;

            var result = validator.Validate(Options.DefaultName, settings);

            Assert.Equal(expectedSuccess, result.Succeeded);
        }

        [Theory]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("ventas", false)]
        [InlineData("ventas:", true)]
        public void PrefixMustExistAndEndWithColon(
       string prefix,
       bool expectedSuccess)
        {
            var settings = CreateValidSettings();
            settings.KeyPrefix = prefix;

            var result = validator.Validate(Options.DefaultName, settings);

            Assert.Equal(expectedSuccess, result.Succeeded);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(3600, true)]
        [InlineData(3601, false)]
        public void TtlMustBeWithinAllowedRange(
       int seconds,
       bool expectedSuccess)
        {
            var settings = CreateValidSettings();
            settings.TtlSeconds = seconds;

            var result = validator.Validate(Options.DefaultName, settings);

            Assert.Equal(expectedSuccess, result.Succeeded);
        }

        [Theory]
        [InlineData(99, 1000, false)]
        [InlineData(100, 1000, true)]
        [InlineData(30000, 1000, true)]
        [InlineData(30001, 1000, false)]
        [InlineData(1000, 99, false)]
        [InlineData(1000, 100, true)]
        [InlineData(1000, 30000, true)]
        [InlineData(1000, 30001, false)]
        public void TimeoutsMustBeWithinAllowedRange(
        int connectTimeout,
        int asyncTimeout,
        bool expectedSuccess)
        {
            var settings = CreateValidSettings();
            settings.ConnectTimeoutMilliseconds = connectTimeout;
            settings.AsyncTimeoutMilliseconds = asyncTimeout;

            var result = validator.Validate(Options.DefaultName, settings);

            Assert.Equal(expectedSuccess, result.Succeeded);
        }
    }
}
