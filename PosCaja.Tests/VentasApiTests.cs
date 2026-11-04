using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PosCaja.Application;

namespace PosCaja.Tests;

public sealed class VentasApiFactory : WebApplicationFactory<Program>
{
    public TestVentaRepository Repository { get; } = new();
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["SqlServer:Server"] = "unused",
                ["SqlServer:Database"] = "pos_caja",
                ["SqlServer:IntegratedSecurity"] = "true",
                ["SqlServer:User"] = "",
                ["SqlServer:Password"] = ""
            }));
        return base.CreateHost(builder);
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IVentaRepository>();
            services.AddSingleton<IVentaRepository>(Repository);
        });
    }
    public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
    });
}

public sealed class VentasApiTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task GetUsesCompositeKeyAndPreservesJsonValues(int idPos)
    {
        using var factory = new VentasApiFactory();
        using var client = factory.CreateHttpsClient();
        var response = await client.GetAsync($"/api/ventas/{TestVentaRepository.VentaId}/{idPos}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"idVenta\"", json);
        Assert.Contains("\"supervisor\":null", json);
        Assert.Contains("\"fechaVenta\":\"2026-11-04T12:00:00\"", json);
        var dto = await response.Content.ReadFromJsonAsync<VentaDto>();
        Assert.Equal(idPos, dto!.IdPos);
        Assert.Equal(123.456m, dto.TotalVendido);
        Assert.Equal(12.345m, dto.PagoCheque);
        Assert.Equal(11.22m, dto.PagoEfectivo);
    }

    [Fact]
    public async Task MissingSaleReturns404ProblemDetails()
    {
        using var factory = new VentasApiFactory();
        using var client = factory.CreateHttpsClient();
        var response = await client.GetAsync($"/api/ventas/{TestVentaRepository.VentaId}/3");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Theory]
    [InlineData("pagina=0")]
    [InlineData("tamanoPagina=101")]
    [InlineData("tamanoPagina=0")]
    [InlineData("pagina=2147483647&tamanoPagina=100")]
    [InlineData("pagina=abc")]
    public async Task InvalidPaginationReturns400WithoutQueryingSql(string query)
    {
        using var factory = new VentasApiFactory();
        using var client = factory.CreateHttpsClient();
        var response = await client.GetAsync("/api/ventas?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(0, factory.Repository.Calls);
    }

    [Fact]
    public async Task EmptyPageUsesDefaultsAndReturns200()
    {
        using var factory = new VentasApiFactory();
        using var client = factory.CreateHttpsClient();
        var response = await client.GetAsync("/api/ventas");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PaginaResultado<VentaDto>>();
        Assert.Empty(page!.Items);
        Assert.Equal(1, page.Pagina);
        Assert.Equal(50, page.TamanoPagina);
        Assert.Equal(0L, page.TotalRegistros);
    }

    [Fact]
    public async Task FailureReturnsSafeProblemDetails()
    {
        using var factory = new VentasApiFactory();
        factory.Repository.Fail = true;
        using var client = factory.CreateHttpsClient();
        var response = await client.GetAsync("/api/ventas");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Password", body);
        Assert.DoesNotContain("private", body);
        Assert.Contains("traceId", body);
    }

    [Fact]
    public async Task OpenApiIncludesBothEndpoints()
    {
        using var factory = new VentasApiFactory();
        using var client = factory.CreateHttpsClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/ventas/{idVenta}/{idPos}", body);
        Assert.Contains("\"/api/ventas\"", body);
    }
}
