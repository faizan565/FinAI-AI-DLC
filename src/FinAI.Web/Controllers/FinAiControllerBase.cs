using FinAI.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Web.Controllers;

public abstract class FinAiControllerBase(FinAiApiClient api) : Controller
{
    protected const string SessionExpiredMessage = "Your session has expired. Please sign in again.";

    protected FinAiApiClient Api { get; } = api;

    protected IActionResult? SessionExpiredRedirect(ApiError? error)
    {
        if (error is not { IsUnauthorized: true })
        {
            return null;
        }

        Api.ForgetSession();
        TempData["Message"] = SessionExpiredMessage;
        return RedirectToAction("Login", "Account");
    }
}
