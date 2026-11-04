using Api.Redis;
using PosCaja.Application;
using PosCaja.Infrastructure.SqlServer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddPosCajaSqlServer(options =>
    builder.Configuration.GetSection(SqlServerOptions.SectionName).Bind(options));

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
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

public partial class Program;
