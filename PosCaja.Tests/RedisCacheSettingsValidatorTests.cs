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

    }
}
