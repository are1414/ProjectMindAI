using Microsoft.AspNetCore.Mvc;

namespace ProjectMind.Web.Infrastructure;

/// <summary>Sayfa controller'ları için ortak bildirim (TempData) yardımcıları.</summary>
public abstract class AppController : Controller
{
    public const string SuccessKey = "Success";
    public const string ErrorKey = "Error";

    protected void Success(string message) => TempData[SuccessKey] = message;

    protected void Error(string message) => TempData[ErrorKey] = message;
}
