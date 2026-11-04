using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PosCaja.Infrastructure.SqlServer;

namespace PosCaja.Tests;

public sealed class SqlServerOptionsTests
{
    private static SqlServerOptions ValidSqlOptions() => new()
    {
        Server = "server,1433",
        Database = "pos_caja",
        User = "reader",
        Password = "test-only"
    };

    [Fact]
    public void SqlAuthenticationPreservesSpecialCharactersAndSecureDefaults()
    {
        var options = ValidSqlOptions();
        options.User = "reader;name";
        options.Password = " secret;\"quoted\"='value' ";
        var result = new SqlConnectionStringBuilder(options.BuildConnectionString());
        Assert.Equal(options.Server, result.DataSource);
        Assert.Equal(options.Database, result.InitialCatalog);
        Assert.Equal(options.User, result.UserID);
        Assert.Equal(options.Password, result.Password);
        Assert.False(result.IntegratedSecurity);
        Assert.Equal(SqlConnectionEncryptOption.Mandatory, result.Encrypt);
        Assert.False(result.TrustServerCertificate);
        Assert.False(result.PersistSecurityInfo);
    }

    [Fact]
    public void IntegratedAuthenticationOmitsCredentialsAndAllowsExplicitTlsSettings()
    {
        var options = new SqlServerOptions
        {
            Server = "local", Database = "pos_caja", IntegratedSecurity = true,
            Encrypt = false, TrustServerCertificate = true
        };
        var result = new SqlConnectionStringBuilder(options.BuildConnectionString());
        Assert.True(result.IntegratedSecurity);
        Assert.False(result.ShouldSerialize("User ID"));
        Assert.False(result.ShouldSerialize("Password"));
        Assert.Equal(SqlConnectionEncryptOption.Optional, result.Encrypt);
        Assert.True(result.TrustServerCertificate);
    }

    [Theory]
    [InlineData("Server")]
    [InlineData("Database")]
    [InlineData("User")]
    [InlineData("Password")]
    public void MissingRequiredFieldProducesSafeValidationError(string field)
    {
        var options = ValidSqlOptions();
        typeof(SqlServerOptions).GetProperty(field)!.SetValue(options, "");
        var exception = Assert.Throws<OptionsValidationException>(() => options.BuildConnectionString());
        Assert.Contains("SqlServer:" + field, exception.Message);
        Assert.DoesNotContain("test-only", exception.Message);
        Assert.DoesNotContain("server,1433", exception.Message);
    }

    [Fact]
    public void MixedAuthenticationIsRejectedWithoutDisclosingCredentials()
    {
        var options = ValidSqlOptions();
        options.IntegratedSecurity = true;
        var exception = Assert.Throws<OptionsValidationException>(() => options.BuildConnectionString());
        Assert.Contains("IntegratedSecurity=true", exception.Message);
        Assert.DoesNotContain(options.Password!, exception.Message);
    }

    [Fact]
    public async Task InvalidOptionsPreventHostStartupEvenWithoutResolvingDbContext()
    {
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddPosCajaSqlServer(_ => { }))
            .Build();
        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public async Task ValidOptionsStartHostAndConfigureDbContextWithoutOpeningConnection()
    {
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddPosCajaSqlServer(options =>
            {
                options.Server = "unused";
                options.Database = "pos_caja";
                options.IntegratedSecurity = true;
            }))
            .Build();
        await host.StartAsync();
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PosCajaDbContext>();
        var connection = new SqlConnectionStringBuilder(context.Database.GetConnectionString());
        Assert.Equal("unused", connection.DataSource);
        Assert.Equal("pos_caja", connection.InitialCatalog);
        Assert.True(connection.IntegratedSecurity);
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
        await host.StopAsync();
    }

    [Fact]
    public void EnvironmentVariablesOverridePreviousConfigurationAndBindDoubleUnderscores()
    {
        // Prefijo único para no leer ni modificar las variables reales del usuario.
        var prefix = "POSCAJA_TEST_" + Guid.NewGuid().ToString("N") + "_";
        var values = new Dictionary<string, string>
        {
            ["SqlServer__Server"] = "environment-server",
            ["SqlServer__Database"] = "pos_caja",
            ["SqlServer__User"] = "environment-user",
            ["SqlServer__Password"] = "environment;password",
            ["SqlServer__IntegratedSecurity"] = "false"
        };
        try
        {
            foreach (var (key, value) in values)
                Environment.SetEnvironmentVariable(prefix + key, value);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["SqlServer:Server"] = "previous-server" })
                .AddEnvironmentVariables(prefix)
                .Build();
            var services = new ServiceCollection();
            services.AddPosCajaSqlServer(options => configuration.GetSection(SqlServerOptions.SectionName).Bind(options));
            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<SqlServerOptions>>().Value;
            var connection = new SqlConnectionStringBuilder(options.BuildConnectionString());
            Assert.Equal("environment-server", connection.DataSource);
            Assert.Equal("environment-user", connection.UserID);
            Assert.Equal("environment;password", connection.Password);
        }
        finally
        {
            foreach (var key in values.Keys)
                Environment.SetEnvironmentVariable(prefix + key, null);
        }
    }
}
