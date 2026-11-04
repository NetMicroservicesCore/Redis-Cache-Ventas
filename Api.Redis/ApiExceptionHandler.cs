using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PosCaja.Application;

namespace Api.Redis;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var validation = exception is PaginacionInvalidaException;
        context.Response.StatusCode = validation ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;
        if (!validation)
            logger.LogError("Fallo procesando la solicitud {TraceId}. Tipo: {ExceptionType}", context.TraceIdentifier, exception.GetType().Name);
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = context.Response.StatusCode,
                Title = validation ? "Paginación inválida" : "Error interno del servidor",
                Detail = validation ? exception.Message : "No fue posible completar la consulta.",
                Extensions = { ["traceId"] = context.TraceIdentifier }
            }
        });
        return true;
    }
}
