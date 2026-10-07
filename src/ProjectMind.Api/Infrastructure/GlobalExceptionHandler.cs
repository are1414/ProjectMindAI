using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ProjectMind.Application.Common;

namespace ProjectMind.Api.Infrastructure;

/// <summary>Uygulama istisnalarını standart ProblemDetails cevabına çevirir.</summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Kayıt bulunamadı"),
            BusinessRuleException => (StatusCodes.Status400BadRequest, "İş kuralı ihlali"),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen hata")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Beklenmeyen hata");

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message
            }
        });
    }
}
