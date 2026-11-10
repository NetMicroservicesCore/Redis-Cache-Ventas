using System.Net;
using Microsoft.Extensions.Options;
using PosCaja.Infrastructure.Redis;

namespace PosCaja.Tests;

public sealed class RedisCacheSettingsTests
{
    private static RedisCacheSettings CreateSettings() => new()
    {
        Enabled = true,
        Host = "redis.example.test",
        KeyPrefix = "tests:ventas:"
    };

    [Fact]
    public void DisabledCacheCannotBuildConnectionConfiguration()
    {
        var settings = new RedisCacheSettings();

        Assert.Throws<InvalidOperationException>(() => settings.BuildConfigurationOptions());
    }

    [Fact]
    public void InvalidSettingsProduceValidationErrorsWithoutSecrets()
    {
        var settings = CreateSettings();
        settings.Host = "";
        settings.Port = 0;
        settings.Password = "test-secret-only";

        var exception = Assert.Throws<OptionsValidationException>(
            () => settings.BuildConfigurationOptions());

        Assert.Equal(typeof(RedisCacheSettings), exception.OptionsType);
        Assert.Contains("Redis:Host es obligatorio.", exception.Failures);
        Assert.Contains("Redis:Port debe estar entre 1 y 65535.", exception.Failures);
        Assert.DoesNotContain(settings.Password, exception.Message);
    }

    [Fact]
    public void DefaultsEnableTlsAndFailFastWithoutCredentials()
    {
        var configuration = CreateSettings().BuildConfigurationOptions();

        var endpoint = Assert.IsType<DnsEndPoint>(Assert.Single(configuration.EndPoints));
        Assert.Equal("redis.example.test", endpoint.Host);
        Assert.Equal(6379, endpoint.Port);
        Assert.Equal(0, configuration.DefaultDatabase);
        Assert.True(configuration.Ssl);
        Assert.Null(configuration.User);
        Assert.Null(configuration.Password);
        Assert.Equal(1000, configuration.ConnectTimeout);
        Assert.Equal(1000, configuration.AsyncTimeout);
        Assert.Equal(0, configuration.ConnectRetry);
        Assert.False(configuration.AbortOnConnectFail);
        Assert.False(configuration.BacklogPolicy.QueueWhileDisconnected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExplicitTransportAndTimeoutSettingsArePreserved(bool ssl)
    {
        var settings = CreateSettings();
        settings.Port = 6380;
        settings.Ssl = ssl;
        settings.ConnectTimeoutMilliseconds = 2300;
        settings.AsyncTimeoutMilliseconds = 4700;

        var configuration = settings.BuildConfigurationOptions();

        var endpoint = Assert.IsType<DnsEndPoint>(Assert.Single(configuration.EndPoints));
        Assert.Equal(6380, endpoint.Port);
        Assert.Equal(ssl, configuration.Ssl);
        Assert.Equal(2300, configuration.ConnectTimeout);
        Assert.Equal(4700, configuration.AsyncTimeout);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("test-user")]
    public void CredentialsPreserveSpecialCharacters(string? user)
    {
        var settings = CreateSettings();
        settings.User = user;
        settings.Password = " test,pass=word;with spaces ";

        var configuration = settings.BuildConfigurationOptions();

        Assert.Equal(user, configuration.User);
        Assert.Equal(settings.Password, configuration.Password);
    }

    [Fact]
    public void ExplicitTlsHostIsPreserved()
    {
        var settings = CreateSettings();
        settings.SslHost = "certificate.example.test";

        var configuration = settings.BuildConfigurationOptions();

        Assert.Equal(settings.SslHost, configuration.SslHost);
    }

    [Fact]
    public void MissingPasswordForExplicitUserPreventsConfiguration()
    {
        var settings = CreateSettings();
        settings.User = "test-user";

        Assert.Throws<OptionsValidationException>(() => settings.BuildConfigurationOptions());
    }
}
