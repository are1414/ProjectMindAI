using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ProjectMind.Application.Common;

namespace ProjectMind.Web.Infrastructure;

/// <summary>Uygulama katmanındaki NotFoundException'ı 404 sayfasına çevirir.</summary>
public sealed class NotFoundExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not NotFoundException)
            return;

        context.Result = new ViewResult { ViewName = "NotFound", StatusCode = StatusCodes.Status404NotFound };
        context.ExceptionHandled = true;
    }
}
