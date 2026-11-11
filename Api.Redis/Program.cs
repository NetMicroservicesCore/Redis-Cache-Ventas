using Api.Redis;
using PosCaja.Application;
using PosCaja.Infrastructure.SqlServer;
using PosCaja.Infrastructure.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, context, cancellationToken) =>
{
    document.Info.Title = "API de ventas";
    document.Info.Version = "v1";
    document.Info.Description = "Consultas de solo lectura a pos_caja.dbo.venta. La identificación de una venta requiere idVenta e idPos.";
    return Task.CompletedTask;
}));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddPosCajaSqlServer(options =>
    builder.Configuration.GetSection(SqlServerOptions.SectionName).Bind(options));

//agregamos la configuración de Redis
builder.Services.AddPosCajaRedis(options =>
    builder.Configuration
        .GetSection(RedisCacheSettings.SectionName)
        .Bind(options));


var app = builder.Build();
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // El manejador registra únicamente información segura.
    SuppressDiagnosticsCallback = _ => true
});
app.UseStatusCodePages();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "Documentación — API de ventas";
        options.SwaggerEndpoint("../openapi/v1.json", "API de ventas v1");
    });
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

public partial class Program;
